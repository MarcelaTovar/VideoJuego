# VR Multiplayer Basketball

Minijuego de basketball multiplayer en VR, construido sobre el **Meta XR Multiplayer Template** de Unity (Netcode for GameObjects + XR Interaction Toolkit). Los jugadores compiten en tiempo real dentro de una misma sesión, con progresión de niveles: solo quien logra el score más alto en cada nivel avanza al siguiente, cada vez más difícil.

## Cómo jugar

1. Entrás a la cancha y presionás **Join**.
2. Te parás en la zona de "ready" para confirmar que estás lista/o.
3. Cuando termina el countdown, arranca la ronda: encestá la pelota en el aro las veces que puedas antes de que se acabe el tiempo.
4. Al terminar, el jugador con el score más alto avanza automáticamente al siguiente nivel. El resto vuelve a la cola para intentar otra ronda del mismo nivel.

## Niveles

| Nivel | Mecánica | Descripción |
|---|---|---|
| 1 | Básico | Aro fijo, +10 puntos por encesto, con detección de swish y power shot según la velocidad del tiro. |
| 2 | Aro en movimiento + viento | El aro se desplaza en dos ejes de forma impredecible, y una zona de viento aplica fuerza aleatoria sobre la pelota en el aire (con un indicador visual de dirección). |
| 3 | Muerte súbita | Un solo fallo termina la ronda inmediatamente — no hay margen de error. |

Al terminar la partida completa, se dispara una secuencia de "portal de victoria" (VFX + succión de la pelota + haptics) como celebración final.

## Sistemas principales

- **Scoring por combo**: encestar varias veces seguidas dentro de una ventana de tiempo aumenta un multiplicador de puntos, con notificación en vivo a todos los jugadores al alcanzar una racha.
- **Detección de calidad de tiro**: swish (collider interno) vs bank shot, y power shot según la velocidad del rigidbody de la pelota al momento de encestar.
- **Feedback sensorial**: partículas, sonido y haptics en los controllers al encestar.
- **Progresión de niveles**: cada cancha (`MiniGameManager`) puede apuntar a la cancha del siguiente nivel; el ganador es admitido ahí automáticamente sin pasar por el flujo normal de "Join".
- **Muerte súbita**: una zona de "fallo" en el piso detecta si la pelota cae sin haber encestado antes, terminando el nivel al instante.

## Arquitectura (scripts propios)

```
Minigame_Basketball.cs       → lógica de score, combo y multiplicador (extiende MiniGameBase del template)
Basketball_target_area.cs    → trigger del aro: detecta encestes, calidad de tiro, ignora la pelota mientras está agarrada
Basketball_MissZone.cs       → trigger de piso para el modo Muerte Súbita (Nivel 3)
BallShotState.cs             → trackea si el tiro actual ya encestó (se resetea al agarrar la pelota de nuevo)
MovingHoop.cs                → mueve el aro en 2 ejes, solo mientras el estado del juego es InGame
WindZone_Basketball.cs       → aplica fuerza aleatoria a la pelota dentro de una zona, con indicador visual
Basketball_Controller.cs     → reset de posición de la pelota
```

Además, se extendió `MiniGameManager.cs` (del template) con:
- `m_LastRoundWinnerId`: guarda el `clientId` del ganador de la ronda.
- `AdmitPlayerDirectly(ulong clientId)`: inscribe a un jugador en una cancha sin pasar por el flujo de UI normal.
- `m_NextLevelManager`: referencia a la cancha del siguiente nivel, para encadenar la progresión.

## Requisitos

- Unity (versión usada en el proyecto — completar con la versión exacta del Editor)
- Meta XR Multiplayer Template (incluye Netcode for GameObjects y XR Interaction Toolkit)
- Meta Quest (probado con Touch Pro) o el simulador de XR Device para desarrollo en editor
- Assets de efectos: [Free Quick Effects Vol. 1](https://assetstore.unity.com) de Gabriel Aguiar Prod (portal de victoria)

## Setup

1. Cloná el repo y abrí el proyecto con la versión de Unity indicada arriba.
2. Instalá el pack de VFX de Gabriel Aguiar si no viene incluido (ver `.gitignore` — los assets de terceros no se versionan).
3. Abrí la escena principal y verificá que cada `MiniGameManager` (Nivel 1, 2, 3) tenga sus referencias asignadas en el Inspector (zonas de teleport, scoreboard, barrera, y `m_NextLevelManager` apuntando al nivel siguiente).
4. Para probar en el editor: activá el XR Device Simulator. Para probar en el headset: conectá por cable (Meta Quest Link) o por Air Link, y usá **Build and Run** desde Unity.

## Problemas conocidos / notas de debugging

- **ADB "Unauthorized device"**: si Unity no logra desplegar al Quest por cable aunque lo detecte, revocá las autorizaciones de USB debugging desde el headset (Developer Tab → MTP Notification) y reconectá. Si el popup de autorización no aparece, usar Meta Quest Developer Hub como alternativa.
- **Ball ownership / parenting en Netcode**: el prefab `Ball` necesita el flag `Transferable` en su `NetworkObject`, y `Sync Parent` desactivado, para evitar warnings al agarrarla/soltarla en multiplayer.
- **Triggers y objetos agarrados**: cualquier trigger que dependa de la posición de la pelota (aro, zona de fallo) debe chequear `XRBaseInteractable.isSelected` para ignorar la pelota mientras está en la mano del jugador — de lo contrario, sostenerla cerca del trigger puede disparar falsos positivos.

## Créditos

- Basado en el **Meta XR Multiplayer Template** (Meta / Unity).
- Efectos VFX del portal de victoria: **Gabriel Aguiar Prod** (Free Quick Effects Vol. 1, Unity Asset Store).

## Roadmap / ideas futuras

- [ ] Desempate por timestamp cuando dos jugadores llegan al mismo score más alto.
- [ ] Doble aro obligatorio (combo espacial) como variante de dificultad.
- [ ] Sincronización de la posición del aro móvil vía `NetworkTransform` si se detectan desfases visules entre jugadores.
- [ ] Versión del ambiente 3D específica por nivel (más oscura/dramática a medida que se avanza).