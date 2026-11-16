# Jefe 2x2 y balance intermedio de Zona 1

Estado: aplicado tras autorización explícita. Al aplicar el paquete, sus 49 archivos preparados coincidieron byte a byte con las copias validadas y se conservaron los metadatos de los assets existentes. El ajuste posterior de habilidades se describe en `SkillStyleBalance.md`.

## Centrado y animaciones del jefe

`BossZone1Combat.prefab` conserva `GridPosition` como origen lógico y sus cuatro casillas ocupadas. El origen del pivote visual se sitúa en el centro del rectángulo 2x2. Se retiran los desplazamientos horizontales superpuestos de `VisualRoot`, `RotationPivot` y el punto de aparición de `LevelBossZ1`.

Las copias de combate bajo `Assets/Animation/Jefe/Combat` reutilizan Idle, Walk, Hit y Dash originales. Sus curvas de posición compensan el desplazamiento horizontal del centro del cuerpo durante cada pose. La rotación del modelo no cambia la raíz lógica ni la ocupación. Los archivos originales de arte permanecen intactos.

`CharacterVisualCentering`, conectado únicamente al jefe, termina de alinear el centro horizontal del cuerpo después de evaluar el Animator. Cubre las mezclas entre poses, cuya interpolación puede descentrar momentáneamente el cuerpo aunque cada clip esté calibrado. Reutiliza un único mesh de muestreo del cuerpo de 204 vértices y no mueve la raíz lógica, los colliders ni el registro de ocupación.

El controlador nuevo conecta `IsWalk`, `Physical Attack` e `IsHit` con `CharacterAnimationController`. Cornada espera el impacto al 55% y el final al 98% del clip; la reproducción completa tarda aproximadamente 0,9 segundos. Embestida utiliza el estado Charge durante el recorrido y vuelve a Idle al terminar o cancelarse. Se incorpora una reacción breve al daño y una retirada de muerte mediante contracción y hundimiento, porque el material original no incluye un clip de muerte del jefe. La muerte espera a completar esa presentación antes de retirar la ocupación.

## Estadísticas aplicadas

Son valores iniciales de dificultad intermedia, no una afirmación de balance definitivo. Se conservan energía, regeneración, costes, alcances, recargas, empujes y pasivas existentes.

| Personaje | Vida anterior → nueva | Ataque anterior → nuevo | Defensa anterior → nueva |
| --- | --- | --- | --- |
| Sir Tete | 20 → 36 | 5 → 6 | 2 → 2 |
| Larva de veneno | 12 → 16 | 4 → 4 | 2 → 1 |
| Variante antigua `Larva Tierra` con veneno | 12 → 16 | 4 → 4 | 2 → 1 |
| Larva de aire | 12 → 14 | 4 → 4 | 2 → 1 |
| Larva de tierra elemental | 12 → 20 | 4 → 5 | 2 → 2 |
| Larva de fuego | 12 → 14 | 4 → 4 | 2 → 1 |
| Jefe | 40 → 72 | 6 → 6 | 2 → 2 |

Los dummies de prueba y la escena de recuperación no forman parte del elenco de combate. No se alteran.

La vida y ataque base de Tete se mantienen iguales en Tutorial, Lobby, Level01–Level10, LevelBossZ1 y NewTest. Las partidas en curso conservan su vida actual: aumentar el máximo no constituye una curación ni modifica los datos de una sesión existente. Para evaluar la nueva resistencia inicial se debe comenzar una partida nueva.

## Daños y resistencia

| Ajuste | Anterior → nuevo |
| --- | --- |
| Bono de Ataque Ígneo | +4 → +1 |
| Daño base de Bola de Fuego | 9 → 5 |
| Daño base de Dash Llameante | 7 → 3 |
| Fórmula de Explosión de Fuego | 18 fijo → Ataque + 6 |
| Veneno enemigo por turno | 2 → 1 |
| Bono directo de la especial de fuego enemiga | 3 → 2 |
| Quemadura de la especial enemiga por turno | 2 → 1 |
| Bono de Cornada | 5 → 3 |
| Bono de Embestida | 7 → 5 |

La Q de fuego aplica ahora 1 de quemadura durante 3 turnos y Explosión aplica 2 durante 2 turnos. Con el nuevo ataque de Tete y sin mejoras, las larvas requieren 3–5 ataques básicos; una Q de fuego con toda su quemadura no elimina por sí sola una larva sana. Explosión conserva su papel de habilidad costosa de área. El ajuste posterior de todos los estilos, incluidas las habilidades de agua y tierra, se detalla en `SkillStyleBalance.md`.

La armadura del jefe conserva su reducción del 20% y su redondeo hacia arriba. Contra Tete sin mejoras, Cornada hace 7 y Embestida 9; dos cargas no derrotan a Tete sano. El ataque físico básico de Tete hace 4 al jefe, que resiste 18 impactos de ese tipo. Es una referencia de resistencia, no una simulación de un combate completo: cascos, terreno, reflejos, mejoras y control alteran el resultado.

## Assets modificados o incorporados

- `Assets/Prefabs/P_Enemigos/Zona_1/Larva.prefab`
- `Assets/Prefabs/P_Enemigos/Zona_1/Larva Tierra.prefab`
- `Assets/Prefabs/P_Enemigos/Zona_1/Larva_Aire.prefab`
- `Assets/Prefabs/P_Enemigos/Zona_1/Larva_Tierra.prefab`
- `Assets/Prefabs/P_Enemigos/Zona_1/Larva_Fuego.prefab`
- `Assets/Prefabs/P_Enemigos/Zona_1/Jefe_Zona1/BossZone1Combat.prefab`
- `Assets/Scenes/Tutorial.unity`, `Assets/Scenes/Zone -1/Lobby.unity`, `Assets/Scenes/Zone -1/Level01.unity` a `Level10.unity`, `Assets/Scenes/Zone -1/LevelBossZ1.unity` y `Assets/Scenes/New-Zone-1/NewTest.unity`.
- Nuevos clips `Boss_Idle`, `Boss_Walk`, `Boss_Attack`, `Boss_Charge`, `Boss_HitReaction`, `Boss_Death` y `BossCombat.controller` bajo `Assets/Animation/Jefe/Combat`, con sus metadatos nuevos.

Se preservan los GUID y `.meta` de todos los assets existentes. No se modifican ScriptableObjects de mejoras, inventario ni recompensas.

Los scripts modificados son `CharacterAnimationController`, `ChargeSkill`, `CharacterStatsData`, `ArmoredLarvaSetup`, `LarvaSkillSetup`, `ElementalLarvaSetup`, `LarvaFireSpitSkill`, `PoisonSpitSkill`, `FireBasicAttackSkill`, `FireballSkill`, `FlameDashSkill` y `FireExplosionSkill`. Se añade `CharacterVisualCentering`. Las pruebas nuevas son `BossCombatPresentationTests` y `CombatBalanceTests`; se ajustan las expectativas de `FireUpgradeTests` y `ElementalLarvaTests`, y se hace explícita la estadística base de la fixture de `TeteUpgradeTests` para mantener aislada la validación de mejoras.

## Validación y límites

Las pruebas nuevas muestrean el cuerpo del jefe en seis estados, seis posiciones temporales y cuatro orientaciones; contemplan su ocupación 2x2, marcha, Cornada, Embestida, cancelación y muerte. EditMode evalúa las poses pero no emite automáticamente AnimationEvents: el test de Cornada entrega los mensajes configurados del clip a su receptor real cuando se alcanza su tiempo. Su emisión automática durante Play Mode queda pendiente. Las de balance ejecutan daño real, estados y ataques básicos y comprueban estadísticas consistentes entre mapas. Se repiten las regresiones de habilidades elementales, cascos, mejoras de Tete, compras y persecución.

Validación inicial de este paquete: compilación correcta y **231 comprobaciones sin fallos** en Unity 6000.3.9f1, en el proyecto aislado. Incluyen 144 combinaciones de pose/orientación y el muestreo adicional de mezclas entre estados. Se verificaron hashes, conservación de objetos existentes, referencias GUID de las escenas y ausencia de conflictos en los nuevos metadatos. El balance posterior de habilidades tiene su propia validación descrita en `SkillStyleBalance.md`.

La compilación y las pruebas del Editor no sustituyen una partida completa. Quedan por comprobar en Play Mode las transiciones visuales del jefe, el encaje vertical con el piso y la dificultad acumulada de una run, usando los distintos cascos y mejoras. Ajustar estos números tras esa prueba si los combates resultan demasiado breves, largos o exigentes.
