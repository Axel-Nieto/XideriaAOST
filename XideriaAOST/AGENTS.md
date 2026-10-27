# XideriaAOST — Reglas de trabajo

Estas instrucciones se aplican a todo el proyecto. Basar las decisiones en el código y las dependencias existentes; distinguir el comportamiento comprobado de las hipótesis y recomendaciones.

## Tecnología y reglas del juego

1. El proyecto utiliza **Unity 6 y C#**. Respetar las convenciones y APIs utilizadas por el proyecto.
2. Mantener la compatibilidad entre personajes **1x1** y enemigos de mayor tamaño, especialmente **2x2**. Considerar el área completa ocupada al analizar colocación, movimiento, rutas, alcance, objetivos, empujes y terreno. No introducir supuestos de una única casilla por personaje.
3. `GridPosition` representa el **origen lógico del área ocupada**, no el centro visual. `Character.GetOccupiedCells(origin)` obtiene las casillas del rectángulo definido por `GridWidth` y `GridHeight`.
4. Las **rotaciones visuales no deben modificar la ocupación lógica del tablero**. Mantener separados la orientación del modelo, los offsets visuales y las coordenadas lógicas.
5. El movimiento y las habilidades **comparten el recurso de energía** de `CharacterStats`. Analizar la coherencia entre cálculo de alcance, selección de rutas, validación y consumo, incluyendo estados y desplazamientos especiales.

## Alcance y protección del proyecto

6. No modificar comportamientos existentes sin analizar sus dependencias, consumidores y efectos sobre los otros sistemas. Revisar tanto el flujo del jugador como el de la IA cuando compartan una regla.
7. No refactorizar sistemas ajenos a la tarea solicitada. Mantener los cambios acotados; informar los problemas adicionales encontrados sin corregirlos fuera del alcance autorizado.
8. Antes de realizar cambios importantes, explicar el problema, el plan, los archivos involucrados, las dependencias afectadas y la validación prevista. Explicar el plan no reemplaza las autorizaciones explícitas requeridas en este documento.
9. Mantener los archivos **`.meta`**, sus GUID y las referencias de Unity. No borrarlos ni regenerarlos innecesariamente. Al agregar, mover o renombrar assets, preservar sus metadatos y revisar el impacto sobre referencias y campos serializados.
10. No modificar **escenas, prefabs ni assets ScriptableObject** sin autorización explícita del usuario. Si una tarea requiere esos cambios, identificar los assets y el motivo antes de solicitar autorización. Esto incluye editar directamente sus archivos serializados.

## Validación y entrega

11. No asumir que una modificación funciona sin validarla. Realizar las comprobaciones pertinentes al cambio; cuando afecte reglas espaciales compartidas, contemplar personajes 1x1 y 2x2. Distinguir revisión estática, compilación, tests y comprobación en Unity. Si una validación no puede realizarse, indicarlo y no presentar el comportamiento como probado.
12. Al terminar, informar los archivos modificados, el comportamiento afectado, la validación realizada, los riesgos y las pruebas pendientes. Indicar expresamente cuando no se hayan modificado archivos.

## Sistemas principales

El código principal está en `Assets/Scripts`. La arquitectura combina componentes `MonoBehaviour`, clases C# de lógica, corrutinas, referencias de escena y eventos estáticos.

| Sistema | Clases principales | Responsabilidad |
| --- | --- | --- |
| Grilla y terreno | `GridSystem`, `GridCell`, `TerrainCellData` | Administrar casillas, terreno, ocupación y conversiones entre tablero y mundo. `CanCharacterOccupy` valida el área completa del personaje. |
| Personajes | `Character`, `CharacterStats`, `CharacterStatsData` | Mantener posición, tamaño, estadísticas, energía, habilidades, cooldowns, estados, pasivas, equipamiento y muerte. |
| Rutas y alcance de movimiento | `AStarPathfinding`, `MovementRangeCalculator` | Buscar rutas cardinales y calcular posiciones alcanzables considerando el espacio requerido por el personaje. |
| Ejecución de movimiento | `CharacterMovement`, `IAction`, `MoveAction` | Ejecutar recorridos mediante corrutinas y actualizar posición y ocupación. `MoveAction` valida y consume energía; la exploración puede omitir ese costo. |
| Habilidades y combate | `SkillBase` y sus derivadas, `PushSystem` | Validar y ejecutar ataques, curación, áreas y desplazamientos; administrar costos y cooldowns. Los empujes consideran el tamaño y las pasivas. |
| Estados y pasivas | `StatusEffect`, `PassiveEffect` y sus derivadas | Procesar efectos por turno y modificar reglas como daño recibido, movimiento e inmunidad al empuje. |
| Turnos | `TurnManager`, `GameEvents` | Coordinar el orden y la resolución de turnos y emitir notificaciones de inicio y finalización. |
| Inteligencia artificial | `IEnemyAI`, `LarvaAI`, `DummyAI`, `LarvaSkillSetup`, `ArmoredLarvaSetup` | Ejecutar turnos enemigos y configurar habilidades y pasivas. `LarvaAI` consulta rutas y habilidades desde posiciones simuladas. |
| Entrada e interfaz | `GridInputHandler`, `BattleHUD` y componentes de `UI` | Conectar controles, selección, previsualizaciones, ejecución de acciones e información del combate. |
| Cascos y recompensas | `HelmetFactory`, `HelmetCatalog`, `HelmetInventoryData`, `HelmetPickup`, `EnemyLootDropper` | Construir equipamiento, cambiar habilidades y pasivas, administrar inventario y recolección, y entregar recompensas. |
| Flujo y sesión | `GameStateManager`, `PlayerSessionData`, `SceneTransitionManager` | Gestionar combate y exploración, victoria y derrota, datos entre escenas y transiciones. |
| Presentación | `CharacterAnimationController`, `SkillVFXSpawner`, `SkillVFXLibrary`, `TileObjectPool` | Coordinar animaciones y efectos y reutilizar objetos de previsualización del tablero. |

## Dependencias que deben revisarse antes de cambiar reglas compartidas

- `TurnManager` emite `GameEvents.OnTurnStarted`; `Character` procesa su inicio de turno y `GridInputHandler` actualiza la interacción y el HUD.
- El movimiento del jugador pasa por `MoveAction` y `CharacterMovement`. La IA llama a `CharacterMovement` y administra su consumo de energía por separado.
- Las habilidades se ejecutan directamente mediante `SkillBase.Execute`, tanto desde la entrada como desde la IA; no todas pasan por `IAction`.
- La validación de una habilidad (`CanUse`), su simulación para IA (`CanUseFromPosition`) y su previsualización (`GetPreviewCells`) son caminos distintos. Revisar su coherencia al modificar reglas.
- La ocupación efectiva está en el registro de `GridSystem`. No asumir que `GridCell.IsOccupied`, `GridOccupancySystem` u otras abstracciones declaradas están integradas: comprobar sus consumidores antes de utilizarlas.
- El análisis existente detectó diferencias entre costos de terreno en A*, presupuesto de pasos y consumo real, además de reglas de terreno y alcance que aún consultan sólo el origen. Son puntos a verificar, no comportamientos que deban corregirse automáticamente fuera de la tarea solicitada.
