using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

#if HAS_MPPM
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif

#if UNITY_EDITOR

#if HAS_PARRELSYNC
using ParrelSync;
#endif

#endif

namespace XRMultiplayer
{
    public class AuthenticationManager : MonoBehaviour
    {
        const string k_DebugPrepend = "<color=#938FFF>[Authentication Manager]</color> ";

        /// <summary>
        /// The argument ID to search for in the command line args.
        /// </summary>
        const string k_playerArgID = "PlayerArg";

        /// <summary>
        /// Determines if the AuthenticationManager should use command line args to determine the player ID when launching a build.
        /// </summary>
        [SerializeField] bool m_UseCommandLineArgs = true;

#if HAS_MPPM

        const string k_MppmEditorName = "-name";
        const string k_MppmCloneProcess = "--virtual-project-clone";

        bool m_IsVirtualPlayer;

        /// <summary>
        /// The XRUIInputModule that is used to control the XRUI -- Cache this value and it with the MPPM virtual players.
        /// </summary>
        public XRUIInputModule inputModule
        {
            get
            {
                if (m_InputModule == null)
                {
                    m_InputModule = FindAnyObjectByType<XRUIInputModule>();
                }
                return m_InputModule;
            }
        }

        XRUIInputModule m_InputModule;
#endif

        /// <summary>
        /// Simple Authentication function. This uses bare bones authentication and anonymous sign in.
        /// </summary>
        /// <returns></returns>
        public virtual async Task<bool> Authenticate()
{
    try
    {
        // Verificar si los servicios generales de Unity no se han iniciado
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            var options = new InitializationOptions();
            string playerId = "Editor_Main";

            // Método universal en Unity para detectar si el proceso actual es un clon de MPPM
            #if UNITY_EDITOR
            string[] args = System.Environment.GetCommandLineArgs();
            bool isCloneInstance = false;
            string cloneName = "";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--virtual-project-clone")
                {
                    isCloneInstance = true;
                }
                if (args[i] == "-name" && (i + 1) < args.Length)
                {
                    cloneName = "_" + args[i + 1];
                }
            }

            if (isCloneInstance)
            {
                // Si el clon no tiene nombre asignado en los argumentos, le inventamos un ID único por defecto
                if (string.IsNullOrEmpty(cloneName)) cloneName = "_Clone_1";
                playerId = $"Editor{cloneName}";
                
                if (inputModule != null)
                {
                    inputModule.enableMouseInput = false;
                    inputModule.enableTouchInput = false;
                }
            }
            #endif

            playerId = SanitizeString(playerId);
            Utils.Log($"{k_DebugPrepend}Iniciando sesión con el perfil limpio: {playerId}");
            options.SetProfile(playerId);

            // Inicializar los servicios de Unity con el perfil asignado
            await UnityServices.InitializeAsync(options);
        }

        // Si el jugador virtual no está autorizado aún, hacemos el Login Anónimo
        if (!AuthenticationService.Instance.IsAuthorized)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // Guardamos el ID de autenticación en el Manager general
        XRINetworkGameManager.AuthenicationId = AuthenticationService.Instance.PlayerId;
        return UnityServices.State == ServicesInitializationState.Initialized;
    }
    catch (System.Exception e)
    {
        Utils.Log($"{k_DebugPrepend}Error crítico durante la autenticación: {e}");
        return false;
    }
}



        public static bool IsAuthenticated()
        {
            try
            {
                return AuthenticationService.Instance.IsSignedIn;
            }
            catch (Exception e)
            {
                Utils.Log($"{k_DebugPrepend}Checking for AuthenticationService.Instance before initialized.{e}");
                return false;
            }
        }

        static string GetPlayerIDArg()
        {
            string playerID = "";
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg.ToLower().Contains(k_playerArgID.ToLower()))
                {
                    var splitArgs = arg.Split(':');
                    if (splitArgs.Length > 0)
                    {
                        playerID += splitArgs[1];
                    }
                }
            }
            return playerID;
        }

        static string SanitizeString(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return string.Empty;
            }

            // Define the regular expression pattern to match characters that are NOT alphanumeric, dash, or underscore.
            // \w matches [a-zA-Z0-9_] (alphanumeric and underscore)
            // \- matches the literal hyphen
            // The caret ^ inside the character set [] negates the set, matching anything NOT in the set.
            string pattern = @"[^\w-]";
            return Regex.Replace(playerId, pattern, "");
        }

#if UNITY_EDITOR
#if HAS_MPPM
        string CheckMPPM()
        {
            Utils.Log($"{k_DebugPrepend}MPPM Found");
            string mppmString = "";

            var arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; ++i)
            {
                if (arguments[i] == k_MppmCloneProcess)
                {
                    m_IsVirtualPlayer = true;
                    inputModule.enableMouseInput = false;
                    inputModule.enableTouchInput = false;
                }
                if (arguments[i] == k_MppmEditorName && (i + 1) < arguments.Length)
                {
                    mppmString += arguments[i + 1];
                }
            }

            if (m_IsVirtualPlayer && string.IsNullOrEmpty(mppmString))
            {
                Utils.LogWarning("An MPPM virtual player was detected, but the Player Name was not set. This may cause authentication failures when trying to connect.");
            }

            return mppmString;
        }

        // This prevents the XRUIInputModule from throwing errors when MPPM is active but focus on the editor has not happened yet.
        void OnApplicationFocus(bool focus)
        {
            // Check to make sure it's an MPPM Virtual Player.
            if (focus && m_IsVirtualPlayer)
            {
                inputModule.enableMouseInput = true;
                inputModule.enableTouchInput = true;
            }
        }
#endif

#if HAS_PARRELSYNC
        string CheckParrelSync()
        {
            Utils.Log($"{k_DebugPrepend}ParrelSync Found");
            string pSyncString = "";
            if (ClonesManager.IsClone()) pSyncString += ClonesManager.GetArgument();
            return pSyncString;
        }
#endif
#endif
    }
}
