using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace XRMultiplayer.MiniGames
{
    /// <summary>
    /// Manages the state of the minigame
    /// </summary>
    public class MiniGameManager : NetworkBehaviour
    {
        /// <summary>
        /// Format for the time text
        /// </summary>
        private const string TIME_FORMAT = "mm':'ss'.'ff";

        /// <summary>
        /// Keeps track of the current game state
        /// </summary>
        public GameState currentNetworkedGameState
        {
            get => networkedGameState.Value;
        }
        public enum GameState { None, PreGame, InGame, PostGame }

        /// <summary>
        /// Keeps track of the current game state synchronized across the network
        /// </summary>
        readonly NetworkVariable<GameState> networkedGameState = new(GameState.PreGame, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Dictionary of players and their assigned scoreboard slots
        /// </summary>
        [NonSerialized]
        public Dictionary<XRINetworkPlayer, ScoreboardSlot> currentPlayerDictionary = new();

        [Tooltip("The current minigame being used")]
        public MiniGameBase currentMiniGame;

        /// <summary>
        /// Determines if the local player is in the game
        /// </summary>
        public bool LocalPlayerInGame => m_LocalPlayerInGame;
        bool m_LocalPlayerInGame = false;

        [Header("UI")]
        public TMP_Text m_GameStateText;
        [SerializeField] TMP_Text m_BestAllText;
        [SerializeField] TMP_Text m_GameNameText;
        [SerializeField, Tooltip("Prefab used for scoreboard ui slots")] GameObject m_PlayerScoreboardSlotPrefab;
        [SerializeField, Tooltip("Prefab used for scoreboard ui slots")] Transform m_ContentListParent;
        [SerializeField] TextButton m_DynamicButton;

        [Header("Video Player")]
        [SerializeField] GameObject m_VideoPlayerObject;
        [SerializeField] GameObject m_TooltipObject;
        [SerializeField] Mask m_TopMask;
        [SerializeField] Mask m_BottomMask;

        [Header("Game")]
        public int maxAllowedPlayers = 4;
        [SerializeField] int m_ReadyUpTimeInSeconds = 15;
        [SerializeField] int m_StartCoutdownTimeInSeconds = 5;
        [SerializeField] int m_PostGameWaitTimeInSeconds = 3;
        [SerializeField] int m_PostGameCountdownTimeInSeconds = 7;
        [SerializeField] GameObject m_TeleportZonesObject;
        [SerializeField] SubTrigger[] m_StartZoneTrigger;
        [Header("Audio")]
        [SerializeField] AudioSource m_GameMusicSource;
        [SerializeField] AudioClip m_GameMusicClip;

        [SerializeField] AudioSource m_WinnerAudioSource;
        [SerializeField] AudioClip m_WinnerAudioClip;

        [SerializeField] float m_WinnerAudioDelay = 0.5f;
        [Header("Winner Celebration")]
        [SerializeField] GameObject m_WinnerCelebration;
        [SerializeField] ParticleSystem m_WinnerConfetti;
        [SerializeField] TMPro.TextMeshProUGUI m_WinnerText;

        [Header("Transform References")]
        [SerializeField] Transform m_ScoreboardTransform;
        [SerializeField] Transform m_ScoreboardInGameTransform;
        [SerializeField] Transform m_JoinTeleportTransform;
        [SerializeField] Transform m_LeaveTeleportTransform;
        [SerializeField] Transform m_FinishTeleportTransform;

        [Header("Transform Offsets")]
        [SerializeField] bool m_UseInGameOffset = true;
        [SerializeField, Tooltip("Determines the offset of the canvas during game")] Vector3 m_InGameOffset;
        [SerializeField, Tooltip("Determines the offset of the canvas during the pre-game")] Vector3 m_PreGameOffset;
        [SerializeField] float m_ScoreboardLerpSpeed = 5.0f;

        [Header("Barrier")]
        [SerializeField] bool m_UseBarrier = true;
        [SerializeField] float m_DistanceCheckTime = .5f;
        [SerializeField] float m_BarrierRenderDistance = 30.0f;
        [SerializeField] Renderer m_BarrierRend;

        readonly List<ScoreboardSlot> m_ScoreboardSlots = new();
        NetworkList<ulong> m_CurrentPlayers;
        NetworkList<ulong> m_QueuedUpPlayers;
        readonly NetworkVariable<float> m_BestAllScore = new(0.0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<ulong> m_LastRoundWinnerId = new(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        [Header("Level Progression")]
        [SerializeField] MiniGameManager m_NextLevelManager;
        TeleportationProvider m_LocalPlayerTeleportProvider;

        float m_CurrentTimer = 0.0f;
        Pose m_ScoreboardStartPose;
        IEnumerator m_StartGameRoutine;
        Coroutine m_PostGameRoutine;

        /// <inheritdoc/>
        void Start()
        {
            if (currentMiniGame == null)
            {
                TryGetComponent(out currentMiniGame);
            }

            m_LocalPlayerTeleportProvider = FindAnyObjectByType<TeleportationProvider>();

            m_TeleportZonesObject.SetActive(false);
            m_BestAllText.text = "<b>Current Record</b>: No Record Set";
            m_ScoreboardStartPose = new Pose(m_ScoreboardTransform.position, m_ScoreboardTransform.rotation);
            m_GameNameText.text = currentMiniGame.gameName;

            foreach (var trigger in m_StartZoneTrigger)
            {
                trigger.OnTriggerAction += TriggerReadyState;
            }

            m_QueuedUpPlayers = new NetworkList<ulong>();
            m_CurrentPlayers = new NetworkList<ulong>();

            if (m_BarrierRend == null)
            {
                m_UseBarrier = false;
            }
            else
            {
                if (m_UseBarrier)
                {
                    StartCoroutine(CheckBarrierRendererDistance());
                }
                else
                {
                    m_BarrierRend.enabled = false;
                }
            }

            SetupPlayerSlots();
        }

        /// <inheritdoc/>
        public virtual void Update()
        {
            if (networkedGameState.Value == GameState.InGame)
            {
                float dt = Time.deltaTime;
                m_CurrentTimer += dt;
                currentMiniGame.UpdateGame(dt);
            }
            if ((networkedGameState.Value == GameState.PreGame || (networkedGameState.Value == GameState.InGame && m_UseInGameOffset)) && LocalPlayerInGame)
            {
                UpdateScoreboardPosition();
            }
        }

        /// <inheritdoc/>
        public override void OnDestroy()
        {
            base.OnDestroy();

            foreach (var trigger in m_StartZoneTrigger)
            {
                trigger.OnTriggerAction -= TriggerReadyState;
            }
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            networkedGameState.OnValueChanged += GameStateValueChanged;
            m_BestAllScore.OnValueChanged += BestAllScoreChanged;
            m_CurrentPlayers.OnListChanged += UpdatePlayerList;
            UpdateBestScore(m_BestAllScore.Value, m_BestAllText);

            if (IsOwner)
            {
                networkedGameState.Value = GameState.PreGame;
                m_BestAllScore.Value = 0;
            }
            UpdateGameState();

            if (networkedGameState.Value == GameState.InGame)
            {
                ResetContestants(true);
            }
        }

        /// <inheritdoc/>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            m_LocalPlayerInGame = false;
            currentPlayerDictionary.Clear();
            m_ScoreboardTransform.SetPositionAndRotation(m_ScoreboardStartPose.position, m_ScoreboardStartPose.rotation);
        }

        private void UpdatePlayerList(NetworkListEvent<ulong> changeEvent)
        {
            if (networkedGameState.Value != GameState.InGame) return;

            // Wipe scoreboard.
            foreach (ScoreboardSlot s in m_ScoreboardSlots)
            {
                s.SetSlotOpen();
            }

            currentPlayerDictionary.Clear();

            foreach (var playerId in m_CurrentPlayers)
            {
                AddPlayerToList(playerId);
            }

            for (int i = currentPlayerDictionary.Count; i < m_ScoreboardSlots.Count; i++)
            {
                m_ScoreboardSlots[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < currentPlayerDictionary.Count; i++)
            {
                m_ScoreboardSlots[i].gameObject.SetActive(true);
                m_ScoreboardSlots[i].UpdateScore(0, currentMiniGame.currentGameType);
            }
        }

        void BestAllScoreChanged(float old, float current)
        {
            if (m_BestAllScore.Value <= 0.0f)
            {
                m_BestAllText.text = $"<b>Current Record</b>: No Record Set";
            }
            else
            {
                if (currentMiniGame.currentGameType == MiniGameBase.GameType.Time)
                {
                    TimeSpan time = TimeSpan.FromSeconds(current);
                    m_BestAllText.text = $"<b>Current Record</b>: {time.ToString(TIME_FORMAT)}";
                }
                else
                {
                    m_BestAllText.text = $"<b>Current Record</b>: {current:N0}";
                }
            }
        }

        void GameStateValueChanged(GameState oldState, GameState currentState)
        {
            UpdateGameState();
        }

        void UpdateGameState()
        {
            switch (networkedGameState.Value)
            {
                case GameState.PreGame:
                    SetPreGameState();
                    break;
                case GameState.InGame:
                    SetInGameState();
                    break;
                case GameState.PostGame:
                    SetPostGameState();
                    break;
            }
        }

        void SetPreGameState()
        {
            m_LocalPlayerInGame = false;
            if (m_PostGameRoutine != null)
            {
                StopCoroutine(m_PostGameRoutine);
            }

            currentMiniGame.SetupGame();
            m_ScoreboardTransform.SetPositionAndRotation(m_ScoreboardStartPose.position, m_ScoreboardStartPose.rotation);

            for (int i = 0; i < m_ScoreboardSlots.Count; i++)
            {
                m_ScoreboardSlots[i].gameObject.SetActive(true);
            }

            ResetContestants(false);

            m_GameStateText.text = "Pre Game";

            m_DynamicButton.UpdateButton(AddLocalPlayer, "Join");
            StartCoroutine(ResetReadyZones());
        }
        void PlayGameMusic()
        {
            if (m_GameMusicSource == null || m_GameMusicClip == null)
                return;

            m_GameMusicSource.clip = m_GameMusicClip;
            m_GameMusicSource.loop = true;
            m_GameMusicSource.Play();
        }

        void SetInGameState()
        {
            m_CurrentTimer = 0.0f;
            ResetContestants(true);
            PlayGameMusic();

            foreach (var slot in currentPlayerDictionary.Values)
            {
                slot.UpdateScore(0.0f, currentMiniGame.currentGameType);
            }

            for (int i = currentPlayerDictionary.Count; i < m_ScoreboardSlots.Count; i++)
            {
                m_ScoreboardSlots[i].gameObject.SetActive(false);
            }

            foreach (var trigger in m_StartZoneTrigger)
            {
                trigger.subTriggerCollider.enabled = false;
            }

            m_GameStateText.text = "In Progess";

            if (LocalPlayerInGame)
            {
                m_DynamicButton.button.interactable = true;
                PlayerHudNotification.Instance.ShowText($"Game Started!");
                ToggleShrink(true);
                if (!m_UseInGameOffset)
                {
                    m_ScoreboardTransform.SetPositionAndRotation(m_ScoreboardInGameTransform.position, m_ScoreboardInGameTransform.rotation);
                }
            }
            else
            {
                m_DynamicButton.button.interactable = false;
            }

            currentMiniGame.StartGame();
        }
        void StopGameMusic()
        {
            if (m_GameMusicSource != null)
            {
                m_GameMusicSource.Stop();
            }
        }
        void ShowWinnerCelebration()
        {
            Debug.Log("🎉 ===== MOSTRANDO CELEBRACIÓN =====");

            if (m_WinnerCelebration != null)
            {
                Debug.Log(
                    $"WinnerCelebration: {m_WinnerCelebration.name} | " +
                    $"activeSelf={m_WinnerCelebration.activeSelf} | " +
                    $"activeInHierarchy={m_WinnerCelebration.activeInHierarchy} | " +
                    $"position={m_WinnerCelebration.transform.position}"
                );

                m_WinnerCelebration.SetActive(true);

                Debug.Log(
                    $"Después de SetActive: " +
                    $"activeSelf={m_WinnerCelebration.activeSelf} | " +
                    $"activeInHierarchy={m_WinnerCelebration.activeInHierarchy}"
                );
            }
            else
            {
                Debug.LogError("❌ m_WinnerCelebration ES NULL");
            }

            if (m_WinnerText != null)
            {
                Debug.Log(
                    $"WinnerText: {m_WinnerText.name} | " +
                    $"activeSelf={m_WinnerText.gameObject.activeSelf}"
                );

                m_WinnerText.text = "¡FELICIDADES POR GANAR!";
                m_WinnerText.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogError("❌ m_WinnerText ES NULL");
            }

            if (m_WinnerConfetti != null)
            {
                Debug.Log(
                    $"Confetti: {m_WinnerConfetti.name} | " +
                    $"activeSelf={m_WinnerConfetti.gameObject.activeSelf}"
                );

                m_WinnerConfetti.gameObject.SetActive(true);
                m_WinnerConfetti.Play();
            }
            else
            {
                Debug.LogError("❌ m_WinnerConfetti ES NULL");
            }
        }
        void HideWinnerCelebration()
        {
            if (m_WinnerConfetti != null)
                m_WinnerConfetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (m_WinnerCelebration != null)
                m_WinnerCelebration.SetActive(false);

            if (m_WinnerText != null)
                m_WinnerText.gameObject.SetActive(false);
        }
        IEnumerator PlayFinalWinnerAudio()
        {
            // Si existe un siguiente nivel, no mostrar celebración final.
            if (m_NextLevelManager != null)
            {
                Debug.Log("➡️ No es nivel final. No hay celebración.");
                yield break;
            }

            if (m_WinnerAudioSource == null || m_WinnerAudioClip == null)
            {
                Debug.LogError("❌ Winner Audio Source o Winner Audio Clip NO están asignados.");
                yield break;
            }

            Debug.Log("🏆 CELEBRACIÓN FINAL: empezando");

            // Pequeña espera opcional antes de comenzar todo.
            yield return new WaitForSeconds(m_WinnerAudioDelay);

            ulong localPlayerId =
                XRINetworkPlayer.LocalPlayer.OwnerClientId;

            Debug.Log(
                $"🏆 Winner ID = {m_LastRoundWinnerId.Value} | " +
                $"Local ID = {localPlayerId}"
            );

            // Solo el ganador ve la celebración.
            if (m_LastRoundWinnerId.Value != localPlayerId)
            {
                Debug.Log("❌ Este jugador NO es el ganador.");
                yield break;
            }

            // ==========================================
            // CELEBRACIÓN + TEXTO + CONFETTI + AUDIO
            // ==========================================

            Debug.Log("🎉 ACTIVANDO WINNER CELEBRATION");

            // Primero hacemos visible TODO.
            ShowWinnerCelebration();

            // Inmediatamente después empieza el audio.
            Debug.Log(
                $"🔊 REPRODUCIENDO AUDIO. Duración = {m_WinnerAudioClip.length} segundos"
            );

            m_WinnerAudioSource.PlayOneShot(m_WinnerAudioClip);

            // Esperamos a que termine el audio.
            yield return new WaitForSeconds(m_WinnerAudioClip.length);

            Debug.Log("🔊 AUDIO TERMINÓ");

            // Dejamos la celebración visible un segundo extra.
            yield return new WaitForSeconds(1f);

            Debug.Log("🎉 OCULTANDO WINNER CELEBRATION");

            HideWinnerCelebration();

            Debug.Log("🏁 CELEBRACIÓN FINAL TERMINADA");
        }
        

        void SetPostGameState()
        {
            StopGameMusic();

            if (LocalPlayerInGame)
            {
                ToggleShrink(false);
                m_BarrierRend.gameObject.SetActive(true);
            }

            m_LocalPlayerInGame = false;
            m_TeleportZonesObject.SetActive(false);

            SortPlayers();

            currentMiniGame.FinishGame(false);

            if (!currentMiniGame.finished)
            {
                currentMiniGame.FinishGame(false);
            }

            // NO mostrar "Post Game" todavía.
            // NO mostrar "Wait" todavía.
            // La coroutine controla toda la secuencia.

            m_PostGameRoutine = StartCoroutine(
                PostGameWithWinnerCelebration()
            );
        }

        IEnumerator PostGameWithWinnerCelebration()
        {
            Debug.Log("🟢 POST GAME: iniciando secuencia completa");

            // ==========================================
            // 1. CELEBRACIÓN
            // ==========================================

            yield return new WaitForSeconds(0.5f);

            Debug.Log("🏆 POST GAME: iniciando celebración");

            yield return StartCoroutine(PlayFinalWinnerAudio());

            Debug.Log("🏁 POST GAME: celebración terminada");

            // ==========================================
            // 2. TELEPORT DESPUÉS DE CELEBRACIÓN
            // ==========================================

            if (LocalPlayerInGame)
            {
                Debug.Log("🚶 TELEPORT: saliendo del área del juego");

                TeleportToArea(m_LeaveTeleportTransform);
            }

            // ==========================================
            // 3. ESPERA ANTES DE NEXT GAME
            // ==========================================

            Debug.Log(
                $"⏳ POST GAME: esperando {m_PostGameWaitTimeInSeconds} segundos"
            );

            yield return new WaitForSeconds(
                m_PostGameWaitTimeInSeconds
            );

            // ==========================================
            // 4. AHORA SÍ MOSTRAR NEXT GAME
            // ==========================================

            Debug.Log("🟡 POST GAME: mostrando NEXT GAME");

            m_GameStateText.text = "Next Game in";

            for (
                int i = m_PostGameCountdownTimeInSeconds;
                i > 0;
                i--
            )
            {
                Debug.Log($"⏱️ NEXT GAME: {i}");

                m_DynamicButton.UpdateButton(
                    ResetGame,
                    $"{i}",
                    true,
                    false
                );

                yield return new WaitForSeconds(1f);
            }

            // ==========================================
            // 5. SIGUIENTE JUEGO
            // ==========================================

            if (IsOwner)
            {
                Debug.Log("🔵 POST GAME: pasando a PRE GAME");

                networkedGameState.Value = GameState.PreGame;
            }

            m_PostGameRoutine = null;
        }

        void TriggerReadyState(Collider other, bool entered)
        {
            if (other.TryGetComponent(out CharacterController controller))
            {
                TogglePlayerReadyRpc(XRINetworkPlayer.LocalPlayer.OwnerClientId, entered);
            }
        }

        [Rpc(SendTo.Everyone)]
        void TogglePlayerReadyRpc(ulong clientId, bool isReady)
        {
            if (XRINetworkGameManager.Instance.TryGetPlayerByID(clientId, out var player))
            {
                if (currentPlayerDictionary.ContainsKey(player))
                {
                    currentPlayerDictionary[player].ToggleReady(isReady);
                }
            }

            if (networkedGameState.Value != GameState.InGame)
            {
                CheckPlayersReady();
            }
        }

        void CheckPlayersReady()
        {
            int readyCount = 0;
            if (m_QueuedUpPlayers.Count <= 0) return;

            foreach (var clientId in m_QueuedUpPlayers)
            {
                if (XRINetworkGameManager.Instance.TryGetPlayerByID(clientId, out var player))
                {
                    if (currentPlayerDictionary.ContainsKey(player))
                    {
                        if (currentPlayerDictionary[player].isReady)
                        {
                            readyCount++;
                        }
                    }
                }
            }

            if (readyCount > 0 && readyCount < m_QueuedUpPlayers.Count)
            {
                if (LocalPlayerInGame)
                {
                    m_DynamicButton.button.interactable = false;
                }
                if (m_StartGameRoutine != null) StopCoroutine(m_StartGameRoutine);
                m_StartGameRoutine = StartGameAfterTime(m_ReadyUpTimeInSeconds);
                StartCoroutine(m_StartGameRoutine);
            }
            else if (readyCount <= 0)
            {
                if (LocalPlayerInGame)
                {
                    m_DynamicButton.button.interactable = true;
                }
                if (m_StartGameRoutine != null) StopCoroutine(m_StartGameRoutine);

                if (LocalPlayerInGame)
                {
                    PlayerHudNotification.Instance.ShowText("Game Start Cancelled");
                }
                m_GameStateText.text = "Pre Game";
            }
            else
            {
                if (LocalPlayerInGame)
                {
                    m_DynamicButton.button.interactable = false;
                }
                if (m_StartGameRoutine != null) StopCoroutine(m_StartGameRoutine);
                m_StartGameRoutine = StartGameAfterTime(m_StartCoutdownTimeInSeconds);
                StartCoroutine(m_StartGameRoutine);
            }
        }
        IEnumerator StartGameAfterTime(int countdownTime)
        {
            for (int i = countdownTime; i > 0; i--)
            {
                m_GameStateText.text = $"Game Starting In {i}";

                if (LocalPlayerInGame)
                {
                    PlayerHudNotification.Instance.ShowText(m_GameStateText.text);
                }
                yield return new WaitForSeconds(1);
            }

            m_GameStateText.text = $"Game Starting Now!";

            if (IsOwner)
            {
                m_DynamicButton.button.interactable = false;
                StartGameOwnerRpc();
            }
        }
        public void AdmitPlayerDirectly(ulong clientId)
        {
            if (!IsOwner) return;

            AddPlayerRpc(clientId);
            if (m_QueuedUpPlayers.Count < maxAllowedPlayers)
            {
                m_QueuedUpPlayers.Add(clientId);
            }
        }

        [Rpc(SendTo.Owner)]
        void StartGameOwnerRpc()
        {
            for (int i = 0; i < m_QueuedUpPlayers.Count; i++)
            {
                m_CurrentPlayers.Add(m_QueuedUpPlayers[i]);
            }
            m_QueuedUpPlayers.Clear();
            networkedGameState.Value = GameState.InGame;
        }

        [Rpc(SendTo.Owner)]
        public void StopGameOwnerRpc()
        {
            // Primero determinamos y guardamos al ganador.
            // Esto debe ocurrir ANTES de cambiar a PostGame.
            if (currentPlayerDictionary.Count > 0)
            {
                SortPlayers();

                // El primer jugador es el ganador.
                // Funciona incluso si tiene 0 puntos.
                m_LastRoundWinnerId.Value =
                    currentPlayerDictionary.Keys.First().OwnerClientId;

                Debug.Log($"🏆 Winner ID: {m_LastRoundWinnerId.Value}");
            }
            else
            {
                m_LastRoundWinnerId.Value = ulong.MaxValue;
            }

            // Ahora sí cambiamos a PostGame.
            networkedGameState.Value = GameState.PostGame;

            // Guardamos el ganador para el siguiente nivel.
            if (m_NextLevelManager != null &&
                m_LastRoundWinnerId.Value != ulong.MaxValue)
            {
                try
                {
                    m_NextLevelManager.AdmitPlayerDirectly(
                        m_LastRoundWinnerId.Value
                    );
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Level Progression] Falló: {e.Message}");
                }
            }

            // Limpiamos los jugadores de esta partida.
            currentPlayerDictionary.Clear();
            m_CurrentPlayers.Clear();
        }
        

        /// <summary>
        /// Submits a player score. If <see cref="finishGameOnScoreSubmit"/> is true, it will finish the game for that player.
        /// This function will also check if all players have finished the game, and if so, will stop the game.
        /// </summary>
        /// <param name="score">The Score to set for the player.</param>
        /// <param name="clientId">Client ID of the player to set the score for.</param>
        /// <param name="finishGameOnScoreSubmit">Whether or not to finish the game on score submit.</param>
        [Rpc(SendTo.Everyone)]
        public void SubmitScoreRpc(float score, ulong clientId, bool finishGameOnScoreSubmit = false)
        {
            if (XRINetworkGameManager.Instance.TryGetPlayerByID(clientId, out XRINetworkPlayer player))
            {
                if (currentPlayerDictionary.ContainsKey(player))
                {
                    currentPlayerDictionary[player].UpdateScore(score, currentMiniGame.currentGameType);
                    if (finishGameOnScoreSubmit)
                    {
                        currentPlayerDictionary[player].isFinished = true;
                        if (player.IsLocalPlayer)
                        {
                            FinishGame();
                        }
                    }
                }
            }

            SortPlayers();
            CheckIfAllPlayersAreFinished();
        }

        void CheckIfAllPlayersAreFinished()
        {
            bool gameOver = true;
            foreach (KeyValuePair<XRINetworkPlayer, ScoreboardSlot> kvp in currentPlayerDictionary)
            {
                if (!kvp.Value.isFinished)
                {
                    gameOver = false;
                    break;
                }
            }

            if (gameOver && IsOwner)
            {
                StopGameOwnerRpc();
            }
        }

        /// <summary>
        /// Called localled on each client when the game is finished.
        /// </summary>
        public void FinishGame()
        {
            if (LocalPlayerInGame)
            {
                ToggleShrink(false);
            }
            StartCoroutine(TeleportAfterFinish());
        }

        IEnumerator TeleportAfterFinish()
        {
            yield return new WaitForSeconds(1.5f);
            if (networkedGameState.Value == GameState.InGame)
            {
                TeleportToArea(m_FinishTeleportTransform);
            }
        }

        /// <summary>
        /// Called from UI Buttons
        /// </summary>
        public void AddLocalPlayer()
        {
            m_DynamicButton.button.interactable = false;
            AddPlayerOwnerRpc(XRINetworkPlayer.LocalPlayer.OwnerClientId);
        }

        [Rpc(SendTo.Owner)]
        void AddPlayerOwnerRpc(ulong clientId)
        {
            AddPlayerRpc(clientId);
            if (m_QueuedUpPlayers.Count < maxAllowedPlayers)
            {
                m_QueuedUpPlayers.Add(clientId);
            }
        }

        [Rpc(SendTo.Everyone)]
        void AddPlayerRpc(ulong clientId)
        {
            if (currentPlayerDictionary.Count < maxAllowedPlayers)
            {
                if (networkedGameState.Value != GameState.PostGame)
                {
                    AddPlayerToList(clientId);
                }

                if (clientId == XRINetworkPlayer.LocalPlayer.OwnerClientId)
                {
                    m_LocalPlayerInGame = true;
                    m_TeleportZonesObject.SetActive(true);
                    m_DynamicButton.UpdateButton(RemoveLocalPlayer, "Leave");

                    TeleportRequest teleportRequest = new()
                    {
                        destinationPosition = m_JoinTeleportTransform.position,
                        destinationRotation = m_JoinTeleportTransform.rotation,
                        matchOrientation = MatchOrientation.TargetUpAndForward
                    };

                    m_LocalPlayerTeleportProvider.QueueTeleportRequest(teleportRequest);
                    Transform destination = GetClosestReadyPosition(m_JoinTeleportTransform.position);
                    m_ScoreboardTransform.rotation = destination.rotation;
                    m_ScoreboardTransform.position = destination.position + (m_ScoreboardTransform.forward + m_PreGameOffset);
                    PlayerHudNotification.Instance.ShowText($"Joined {currentMiniGame.gameName}");
                    m_BarrierRend.gameObject.SetActive(false);
                }

                if (currentPlayerDictionary.Count >= maxAllowedPlayers & !LocalPlayerInGame && networkedGameState.Value != GameState.PostGame)
                {
                    m_DynamicButton.button.interactable = false;
                }
            }
        }

        void AddPlayerToList(ulong clientId)
        {
            if (XRINetworkGameManager.Instance.TryGetPlayerByID(clientId, out XRINetworkPlayer player))
            {
                if (!currentPlayerDictionary.ContainsKey(player))
                {
                    ScoreboardSlot slot = m_ScoreboardSlots[currentPlayerDictionary.Count];
                    currentPlayerDictionary.Add(player, slot);
                    slot.SetupPlayerSlot(currentPlayerDictionary.Count, player.playerName);
                    player.onDisconnected += PlayerDisconnected;
                }
            }
        }

        /// <summary>
        /// Called from UI buttons
        /// </summary>
        public void RemoveLocalPlayer()
        {
            m_DynamicButton.UpdateButton(AddLocalPlayer, "Join", false, false);
            RemovePlayerOwnerRpc(XRINetworkPlayer.LocalPlayer.OwnerClientId);
        }

        [Rpc(SendTo.Owner)]
        void RemovePlayerOwnerRpc(ulong clientId)
        {
            RemovePlayerRpc(clientId);

            if (m_QueuedUpPlayers.Contains(clientId))
            {
                m_QueuedUpPlayers.Remove(clientId);
            }

            if (m_CurrentPlayers.Contains(clientId))
            {
                m_CurrentPlayers.Remove(clientId);
            }
        }

        [Rpc(SendTo.Everyone)]
        void RemovePlayerRpc(ulong clientId)
        {
            if (XRINetworkGameManager.Instance.TryGetPlayerByID(clientId, out XRINetworkPlayer player))
            {
                CheckDroppedPlayer(player);
            }

            if (clientId == XRINetworkPlayer.LocalPlayer.OwnerClientId)
            {
                m_LocalPlayerInGame = false;
                m_TeleportZonesObject.SetActive(false);

                //If local player left, and we are still in game, don't let them rejoin mid game.
                if (networkedGameState.Value != GameState.InGame)
                {
                    m_DynamicButton.button.interactable = true;
                }

                ToggleShrink(false);
                currentMiniGame.RemoveInteractables();
                PlayerHudNotification.Instance.ShowText($"Left {currentMiniGame.gameName}");
                TeleportToArea(m_LeaveTeleportTransform);
                m_ScoreboardTransform.SetPositionAndRotation(m_ScoreboardStartPose.position, m_ScoreboardStartPose.rotation);
                m_BarrierRend.gameObject.SetActive(true);
            }
        }

        private void PlayerDisconnected(XRINetworkPlayer droppedPlayer)
        {
            CheckDroppedPlayer(droppedPlayer);
        }

        void CheckDroppedPlayer(XRINetworkPlayer droppedPlayer)
        {
            ScoreboardSlot removedSlot = null;
            if (currentPlayerDictionary.ContainsKey(droppedPlayer) && networkedGameState.Value != GameState.PostGame)
            {
                removedSlot = currentPlayerDictionary[droppedPlayer];
                removedSlot.SetSlotOpen();
                currentPlayerDictionary.Remove(droppedPlayer);
                droppedPlayer.onDisconnected -= PlayerDisconnected;

                SortPlayers();
            }

            if (IsOwner && m_QueuedUpPlayers.Contains(droppedPlayer.OwnerClientId))
            {
                m_QueuedUpPlayers.Remove(droppedPlayer.OwnerClientId);
            }

            if (networkedGameState.Value == GameState.InGame)
            {
                if (removedSlot != null)
                {
                    removedSlot.gameObject.SetActive(false);
                }

                if (currentPlayerDictionary.Count <= 0)
                {
                    m_DynamicButton.button.interactable = false;
                    if (IsOwner)
                    {
                        StopGameOwnerRpc();
                    }
                }
                else
                {
                    CheckIfAllPlayersAreFinished();
                }
            }
            else if (networkedGameState.Value == GameState.PreGame)
            {
                if (currentPlayerDictionary.Count > 0)
                {
                    if (currentPlayerDictionary.Count >= maxAllowedPlayers)
                    {
                        m_DynamicButton.button.interactable = false;
                    }
                    else
                    {
                        m_DynamicButton.button.interactable = true;
                    }
                }
                CheckPlayersReady();
            }
        }
        void SortPlayers()
        {
            if (currentMiniGame.currentGameType == MiniGameBase.GameType.Time)
            {
                currentPlayerDictionary = currentPlayerDictionary.OrderBy(x => x.Value.currentScore).ToDictionary(x => x.Key, x => x.Value);
            }
            else
            {
                currentPlayerDictionary = currentPlayerDictionary.OrderByDescending(x => x.Value.currentScore).ToDictionary(x => x.Key, x => x.Value);
            }
            OrganizePlayerList();
        }

        void OrganizePlayerList()
        {
            int currentPlace = 1;
            foreach (var slot in currentPlayerDictionary.Values)
            {
                slot.transform.SetSiblingIndex(currentPlace - 1);
                slot.UpdatePlace(currentPlace++);
            }

            m_ScoreboardSlots.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        }

        void ToggleShrink(bool toggle)
        {
            m_BottomMask.enabled = toggle;
            m_BottomMask.graphic.enabled = toggle;

            m_TopMask.enabled = !toggle;
            m_TopMask.graphic.enabled = !toggle;
            m_GameNameText.enabled = !toggle;
            m_VideoPlayerObject.SetActive(!toggle);
            m_TooltipObject.SetActive(!toggle);
        }

        IEnumerator CheckBarrierRendererDistance()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(m_DistanceCheckTime);
                if (m_UseBarrier)
                {
                    m_BarrierRend.enabled = Vector3.Distance(m_BarrierRend.transform.position, Camera.main.transform.position) < m_BarrierRenderDistance;
                }
            }
        }

        void UpdateScoreboardPosition()
        {
            Vector3 offset = networkedGameState.Value == GameState.InGame ? m_InGameOffset : m_PreGameOffset;
            Transform destination = GetClosestReadyPosition(XRINetworkPlayer.LocalPlayer.transform.position);
            m_ScoreboardTransform.rotation = destination.rotation;
            Vector3 destinationPosition = destination.position + (m_ScoreboardTransform.right * offset.x) + (m_ScoreboardTransform.up * offset.y) + (m_ScoreboardTransform.forward * offset.z);
            m_ScoreboardTransform.position = Vector3.Lerp(m_ScoreboardTransform.position, destinationPosition, Time.deltaTime * m_ScoreboardLerpSpeed);
        }

        Transform GetClosestReadyPosition(Vector3 position)
        {
            Transform closestTransform = null;

            foreach (var readyZone in m_StartZoneTrigger)
            {
                if (closestTransform == null || Vector3.Distance(readyZone.transform.position, position) < Vector3.Distance(closestTransform.position, position))
                {
                    closestTransform = readyZone.transform;
                }
            }

            return closestTransform;
        }

        /// <summary>
        /// Updates the player scores based on <see cref="m_CurrentTimer"/>.
        /// </summary>
        public void UpdatePlayerScores()
        {
            foreach (var p in currentPlayerDictionary)
            {
                if (!p.Value.isFinished)
                {
                    p.Value.UpdateScore(m_CurrentTimer, currentMiniGame.currentGameType);
                }
            }
        }

        /// <summary>
        /// Resets the Game State
        /// </summary>
        /// <remarks>
        /// This function is called locally at times, which creates a divergence between local game state and network game state
        /// </remarks>
        void ResetGame()
        {
            networkedGameState.Value = GameState.PreGame;
            SetPreGameState();
        }

        IEnumerator ResetReadyZones()
        {
            yield return new WaitForSeconds(1.0f);
            foreach (var trigger in m_StartZoneTrigger)
            {
                trigger.subTriggerCollider.enabled = true;
            }
        }

        void ResetContestants(bool showGamePlayers)
        {
            // Wipe scoreboard.
            foreach (ScoreboardSlot s in m_ScoreboardSlots)
            {
                s.SetSlotOpen();
            }

            currentPlayerDictionary.Clear();

            if (showGamePlayers)
            {
                // Add all contestants in current match.
                foreach (var playerId in m_CurrentPlayers)
                {
                    AddPlayerToList(playerId);
                }
            }
            else
            {
                // Add all contestants in queue.
                foreach (var playerId in m_QueuedUpPlayers)
                {
                    AddPlayerToList(playerId);
                }
            }
        }

        void TeleportToArea(Transform teleportTransform)
        {
            TeleportRequest teleportRequest = new TeleportRequest
            {
                destinationPosition = teleportTransform.position,
                destinationRotation = teleportTransform.rotation,
                matchOrientation = MatchOrientation.TargetUpAndForward
            };

            m_LocalPlayerTeleportProvider.QueueTeleportRequest(teleportRequest);
        }

        void UpdateBestScore(float score, TMP_Text textAsset)
        {
            if (m_BestAllScore.Value <= 0.0f)
            {
                textAsset.text = $"<b>Current Record</b>: No Record Set";
            }
            else
            {
                if (currentMiniGame.currentGameType == MiniGameBase.GameType.Time)
                {
                    if (score <= m_BestAllScore.Value && m_BestAllScore.Value > 0.0f)
                    {
                        TimeSpan time = TimeSpan.FromSeconds(score);
                        textAsset.text = $"<b>Current Record</b>: {time.ToString(TIME_FORMAT)}";
                    }
                }
                else
                {
                    if (score >= m_BestAllScore.Value && m_BestAllScore.Value > 0.0f)
                    {
                        textAsset.text = $"<b>Current Record</b>:  {score:N0}";
                    }
                }
            }
        }

        void SetupPlayerSlots()
        {
            for (int i = 0; i < maxAllowedPlayers; i++)
            {
                Instantiate(m_PlayerScoreboardSlotPrefab, m_ContentListParent).TryGetComponent(out ScoreboardSlot slot);
                m_ScoreboardSlots.Add(slot);
                slot.SetSlotOpen();
            }
        }
        [Rpc(SendTo.Everyone)]
        public void AnnounceSuddenDeathFailRpc()
        {
            PlayerHudNotification.Instance.ShowText("💥 ¡Fallaste! Muerte súbita.");
        }
    }
}
