# Recompensas por puerta — etapa 2

El código está integrado, pero no se editaron escenas, prefabs ni ScriptableObjects. Configurar lo siguiente manualmente **antes de entrar en Play Mode**, porque una oferta existente conserva su resultado aunque después cambie el Inspector.

## Configuración en Unity

En el componente **GridSystem** de cada escena de origen, configurar **Door room rewards**:

- **Combat Reward Scenes**: nombres exactos de los destinos que son salas de combate con recompensa. No incluir Lobby, Tutorial, Inicio ni salas vacías.
- **Door Rewards**: pesos iniciales Gold = 1 y Xideria Fragments = 1. Definir cantidades positivas de prueba para los tipos habilitados. Las cantidades quedan inicialmente en cero para no imponer valores de balance; configurar la lista de combate sin cantidades válidas deshabilita esa salida y muestra un error.
- **Gold Reward Sprite** y **Xideria Fragments Reward Sprite**: asignar los sprites correspondientes. No se identificaron sprites de monedas por nombre o metadatos en los assets actuales. Si existen dentro de un atlas, asignar esos sprites; de lo contrario importar/asignar los recursos manualmente como Sprite.
- **Reward Icon Anchor**, en Terrain Cells: asignar el Transform de Cartel para fijar el icono a su superficie. Las dos casillas pueden compartir el mismo anclaje o tenerlo asignado solo en una. Un mismo ID de puerta no admite anclajes distintos.
- **Reward Icon Offset**: posición local respecto del anclaje. Sin anclaje, desplazamiento en coordenadas del mundo desde el centro de las casillas de la puerta. Dejar una pequeña separación con la cara del cartel para evitar parpadeos.
- **Reward Icon World Size**, **Reward Icon Rotation Offset** y **Reward Icon Sorting Order**: ajustar tamaño, rotación fija relativa al anclaje (ángulos del mundo si no hay anclaje) y orden del sprite. El icono no sigue a la cámara. Comprobar su visibilidad frente al escenario en Play Mode.

En `Assets/Scenes/Zone -1/Lobby.unity`, las casillas (11,5) y (11,6) están vinculadas al Cartel agregado bajo Zona_1. Su posición local de icono es (0, 0.0162, 0.26) y su rotación local (0, 180, 0). Esa rotación orienta la cara del sprite sobre la cara visible del cartel, sin giro en Z. El SpriteRenderer queda como hijo del cartel y acompaña sus movimientos; el tamaño continúa expresado en unidades del mundo al crearlo.

Estos son los assets exactos y los destinos actuales que corresponde habilitar:

| Asset de origen | Combat Reward Scenes |
| --- | --- |
| `Assets/Scenes/Zone -1/Lobby.unity` | `Level01`, `Level02` |
| `Assets/Scenes/Zone -1/Level01.unity` | `Level03` |
| `Assets/Scenes/Zone -1/Level02.unity` | `Level03` |
| `Assets/Scenes/Zone -1/Level03.unity` | `Level04`, `Level05` |
| `Assets/Scenes/Zone -1/Level04.unity` | `LevelBossZ1` |
| `Assets/Scenes/Zone -1/Level05.unity` | `LevelBossZ1` |
| `Assets/Scenes/Zone -1/Level06.unity` | `Level03` |
| `Assets/Scenes/Zone -1/Level07.unity` | `Level04`, `Level05` |
| `Assets/Scenes/Zone -1/Level08.unity` | Vacío: retorno al Lobby |
| `Assets/Scenes/Zone -1/Level09.unity` | Vacío: retorno al Lobby |
| `Assets/Scenes/Zone -1/Level10.unity` | `Level03` |
| `Assets/Scenes/Zone -1/LevelBossZ1.unity` | Vacío: retorno al Lobby |
| `Assets/Scenes/Tutorial.unity` | Vacío: salida del tutorial |

Solo hacen falta sprites y cantidades donde se habilitan destinos de combate. Los destinos deben estar habilitados en la lista de escenas del Build Profile. Actualmente están habilitados Inicio, Lobby, Tutorial, Level01–Level05 y LevelBossZ1; si se desea iniciar/cargar Level06–Level10, agregarlos manualmente al perfil.

No hay que modificar `Assets/DungeonCellHighlight.prefab` ni `Assets/TutorialCellHighlight.prefab`. Los indicadores existentes se mantienen y el icono se crea como un SpriteRenderer durante la ejecución, uno por puerta lógica, fijado a su anclaje o al mundo.

## Puertas lógicas

El sistema agrupa salidas cardinalmente adyacentes, del mismo tipo y con la misma lista ordenada de destinos. Por ejemplo, las casillas (11,5) y (11,6) del Lobby son una única puerta, con ID automático `exit:5:11,5`.

El campo opcional **Logical Door Id** de cada elemento de Terrain Cells permite una identidad explícita: usar el mismo ID para las dos casillas de una puerta. Para puertas independientes contiguas, usar IDs diferentes. Las casillas con un mismo ID deben compartir tipo y destinos; una inconsistencia deshabilita esa puerta. Conviene establecer IDs explícitos antes de comenzar la run si se prevé mover las puertas en el editor.

El contacto utiliza `Character.GetOccupiedCells`, con el origen lógico y sin offsets ni rotaciones visuales. Una ocupación 2x2 puede activar la puerta por cualquiera de sus cuatro casillas. Si toca dos puertas distintas simultáneamente, no se elige ninguna automáticamente.

## Estado, transición y entrega

Cada oferta conserva en RunState el origen, puerta, identidad y escena destino, moneda, cantidad y marca de entrega. Los destinos no recompensados también se fijan, con HasReward = false. Las consultas y la recreación de iconos no sortean nada.

La elección registra una sala pendiente antes de iniciar la carga. Otra solicitud de transición se rechaza mientras existe esa elección. Al entrar se consume la identidad pendiente; una recarga conserva la sala actual. Una vuelta al Lobby reutiliza su identidad y las ofertas anteriores. Los atajos de depuración que cargan otra escena directamente no seleccionan una oferta ni conceden premios.

GameStateManager solo solicita la entrega cuando hubo enemigos vivos al iniciar la sala, ya no quedan enemigos que impidan la victoria y el jugador sigue vivo. Se usa PlayerSessionData.TryAddCurrency. La marca se reserva antes de BalanceChanged para impedir un pago reentrante o repetido, incluso si un suscriptor lanza una excepción después del crédito. Si la suma se rechaza por capacidad, el saldo queda intacto y la oferta permanece pendiente, sin pago parcial; se emite una advertencia.

ResetSession no borra estos registros ni monedas. Morir no entrega la recompensa: volver por la misma oferta permite reintentar la misma sala. Repetir una sala ya pagada no vuelve a acreditar monedas. Solo AbandonRun borra la run; continúa sin estar conectado a Escape ni menús. Para probar una configuración nueva, detener Play Mode o abandonar explícitamente la run y recargar la escena; no esperar que una oferta existente cambie al editar el Inspector.

No hay guardado en disco: cerrar el proceso pierde el estado en memoria. RunState contiene valores e identidades serializables, sin referencias a objetos de escena.

## Validación en Unity pendiente

1. Ejecutar RunStateTests, DoorRewardTests y RoomRewardDeliveryTests en Test Runner / EditMode; incluyen los recorridos con JsonUtility.
2. Iniciar desde Lobby o Tutorial (ya contienen PlayerSessionData), configurar cantidades de prueba y sprites y verificar un único icono estampado sobre cada cartel. Mover la cámara: el sprite debe conservar su orientación respecto del cartel. Mover/rotar el cartel: el sprite debe acompañarlo.
3. Anotar tipo, cantidad, RewardId y destino en el depurador. Recrear los indicadores y recargar la escena: no deben cambiar. Entrar, ganar y comprobar el saldo contra esa oferta; el HUD de monedas sigue pendiente de la etapa 3.
4. Repetir la comprobación de victoria, recargar una sala completada, morir antes de ganar y volver por la puerta, volver al Lobby tras ganar y repetir solicitudes de transición. Comprobar ausencia de pérdidas y de pagos duplicados.
5. Probar las salidas del tutorial y los retornos de Level08, Level09 y LevelBossZ1: no deben mostrar icono de recompensa ni pagar por la transición.
6. Probar jugadores 1x1 y 2x2, incluida una ocupación cuyo origen está fuera de la puerta pero cuyo borde la toca. Probar dos puertas distintas tocadas a la vez: ninguna debe elegirse automáticamente.
7. Probar pesos nulos/negativos, cantidades inválidas, destinos ausentes del Build Profile, sprites faltantes, IDs o anclajes inconsistentes y saldo int.MaxValue. No debe haber sorteos de reemplazo ni pagos parciales.

Los tests administrados externos prueban las reglas usando el código compilado; no ejecutan Awake/Start, transiciones reales, JsonUtility, representación de sprites ni animación del movimiento. Esas comprobaciones requieren Unity con una licencia válida.
