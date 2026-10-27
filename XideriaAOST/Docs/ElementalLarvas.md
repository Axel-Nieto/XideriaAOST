# Larvas elementales de Zona 1

Prefabs de combate nuevos en `Assets/Prefabs/P_Enemigos/Zona_1`:

| Prefab | Especial | Alcance | Efecto |
| --- | --- | --- | --- |
| `Larva_Aire.prefab` | Arrastre de Aire | 4–6 | Daño físico y arrastre de una casilla hacia la larva; requiere alineación cardinal entre las áreas ocupadas. |
| `Larva_Tierra.prefab` | Cadenazo | 1–2 | Daño físico y empuje de una casilla, con las reglas existentes de inmunidad y reflexión. |
| `Larva_Fuego.prefab` | Escupitajo de Fuego | 3–4 | Daño de fuego a un único objetivo y burn. |

Los tres conservan estadísticas, mordida, movimiento, IA, indicador y recompensas de `Larva.prefab`, que usa el modelo de veneno. No se agregaron pasivas nuevas. Los prefabs y las escenas anteriores se conservan. Colocar cada prefab y configurar `startGridPosition` en Character; su valor inicial es `(0, 0)` y su ocupación es 1×1. El tamaño y la rotación del modelo no cambian esa ocupación.

Valores iniciales de las especiales: 3 de energía y 3 turnos de recarga. Aire y tierra usan el daño físico básico (`max(1, Attack - Defense)`). Fuego usa `max(1, 3 + Attack - Defense)` y aplica el `BurnEffect` existente durante 3 turnos, con 2 de daño por turno. Los valores no especificados en el pedido siguen el patrón del escupitajo venenoso y son editables en ElementalLarvaSetup. La IA prioriza la especial y usa la mordida cuando corresponde, incluso durante la recarga.

Los modelos se conservan como instancias de los prefabs entregados en Models/Enemigos/Zona_1. Los tres prefabs visuales `P_LarvaAire`, `P_LarvaTierra` y `P_LarvaFuego` asignan materiales distintos al cuerpo: aire usa el celeste de `M_EnemyLarva 1`, tierra el ocre de `M_EnemyLarva 2` y fuego el naranja de `M_EnemyLarva_Fuego`. Veneno conserva el verde de `M_EnemyLarva`. Esta asignación la heredan los prefabs de combate y sus apariciones aleatorias; los materiales de los accesorios se conservan.

En los tres prefabs de combate elementales, la posición Y local del modelo visual es `0.15` (antes `-0.13`). Con la escala raíz `0.7`, el ajuste lo eleva `0.196` unidades de mundo. Este offset interno se conserva aunque el spawner configure el offset general de Character y durante el movimiento. Veneno conserva su altura original.

Los controladores adaptados están en `ElementalAnimations`: parámetros compatibles con CharacterAnimationController, estado `Death` y clips propios de cada variante. Las copias de Hit y Skill tienen eventos de impacto al 45% y de finalización al 98%; las transiciones de salida esperan al final para no perder dichos eventos. Las copias de Spawn eliminan el evento vacío de los originales. Los archivos FBX, controladores y clips originales no se modifican. El proyectil de fuego reutiliza SkillVFXLibrary; sin biblioteca visual, el daño y burn siguen resolviéndose.

Validación: compilación, referencias de assets y pruebas de Editor en `ElementalLarvaTests`. La compilación de las pruebas no equivale a ejecutarlas. Comprobar en Unity Test Runner los límites de alcance, alineación simulada, ocupación 1×1/2×2, destino bloqueado, inmunidad, gasto único de energía, recargas, burn y conexiones de prefabs. En Play Mode comprobar escala/orientación, caminar, mordida, especial, impacto visual, muerte y devolución del turno a la IA. Los tiempos de impacto iniciales necesitan revisión visual con las animaciones reales.

La revisión de dependencias detectó una referencia antigua a una textura inexistente en `M_EnemyLarva.mat`, guardada en `_Texture2DAsset_ab86c241a93a4ff9a5b7610711bf1bb3_Out_0_Texture2D`. Esa propiedad no está declarada en el shader actual. Se conservó el material compartido y se dejó esta limpieza fuera del cambio.
