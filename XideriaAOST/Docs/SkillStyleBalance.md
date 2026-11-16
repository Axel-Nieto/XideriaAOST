# Daño y estilos de juego de los cascos

Este ajuste continúa el balance intermedio de `BossAndCombatBalance.md`. Se modifican daños y textos de habilidades; se conservan estadísticas de personajes, costes, alcances, áreas, recargas, empujes, curación, escudos y duración de los estados.

## Identidad de cada estilo

- Sin casco: ataque cercano y directo que sirve de referencia.
- Fuego: más daño a distancia, daño de área y quemadura. La quemadura de la Q deja de dominar por sí sola el daño de una habilidad barata, y el dash prioriza su movilidad sobre el daño de una especial estática.
- Agua: daño moderado acompañado por empuje, ralentización, inmovilización y curación. Prisión Marina gana escalado de ataque para que las mejoras también beneficien a este estilo.
- Tierra: la Q cede algo de daño por su área, empuje y acumulación de armadura. La R gana un golpe más potente que la Q y conserva la creación de rocas para controlar el tablero.

## Valores actuales

`A` es el ataque actual de Tete, incluidas las mejoras compradas. El daño directo se calcula como `max(1, A + modificador - defensa del objetivo)` y después se aplican las pasivas defensivas existentes. Los valores de ejemplo son anteriores a defensa y armadura, con `A = 6`.

| Habilidad | Fórmula directa | Ejemplo | Energía | Recarga | Utilidad adicional |
| --- | --- | ---: | ---: | ---: | --- |
| Ataque Básico | A | 6 | 2 | 0 | Cuerpo a cuerpo |
| Ataque Ígneo · Q | A + 1 | 7 | 2 | 0 | Quemadura: 1 × 3 turnos |
| Bola de Fuego · W | A + 5 | 11 | 4 | 2 | Área; conserva su mejora de recarga |
| Dash Llameante · E | A + 3 | 9 | 3 | 2 | Desplazamiento y golpe en la llegada; conserva su mejora de recarga |
| Explosión de Fuego · R | A + 6 | 12 | 6 | 3 | Área amplia; quemadura: 2 × 2 turnos |
| Chorro de Agua · Q | A | 6 | 2 | 0 | Empuje de 1 casilla |
| Marea · E | A + 2 | 8 | 5 | 2 | Reposición, daño en franja y ralentización |
| Prisión Marina · R | A + 1 | 7 | 5 | 4 | Inmovilización |
| Giro de Tierra · Q | A − 1 | 5 | 2 | 0 | Área, empuje y +10% de armadura por enemigo golpeado |
| Erupción de Tierra · R | A + 3 | 9 | 6 | 4 | Área, empuje y hasta 3 rocas temporales |

Agua Curativa sigue curando 8 por 4 de energía y 3 turnos de recarga. Escudo de Roca y Excavar mantienen sus costes, cargas y efectos. No se añade daño a estas habilidades.

La quemadura tiene sus propios ticks y no vuelve a restar la defensa plana; sí atraviesa el procesamiento de pasivas de `Character.TakeDamage`, igual que antes. Una mejora de ataque aumenta en 2 el daño directo previo a mitigación de todas las habilidades ofensivas, incluidas ahora Explosión y Prisión Marina. No aumenta el daño fijo por turno de los estados.

Los contadores internos de Slow y Root siguen siendo 3. `Character` los decrementa y retira al inicio de turno, antes de actuar: afectan las dos acciones de turno siguientes y se retiran antes de la tercera. Los tooltips de Marea y Prisión reflejan esos dos turnos efectivos; no se cambió la duración interna.

## Archivos y comprobación

Scripts ajustados: `FireBasicAttackSkill`, `FlameDashSkill`, `FireExplosionSkill`, `WaterWaveSkill`, `WaterPrisonSkill`, `RockSkillBase`, `RockBasicSkill` y `RockEruptionSkill`. Bola de Fuego, Chorro de Agua y Ataque Básico conservan su daño y participan en la comparación. Los tooltips muestran el ataque actual y los nuevos valores de quemadura o control.

`SkillStyleBalanceTests` ejecuta las diez habilidades ofensivas contra objetivos 1x1 y 2x2, con ataque 6 y 8, defensa y armadura. Comprueba daño directo único por personaje, ticks de quemadura, aliados, gasto de energía, recargas, desplazamientos, tooltips, duración efectiva del control y daño mínimo ante defensa alta. Se actualizan los daños esperados en las regresiones de giro, erupción y dash; sus comprobaciones de espacio, pasivas y mejoras se conservan.

Resultado: compilación correcta y **244 comprobaciones sin fallos** en Unity 6000.3.9f1, en un proyecto aislado. La comparación principal recorre 80 combinaciones de habilidad, ataque, tamaño y armadura. Los 35 archivos de escenas, prefabs y animaciones del paquete anterior conservan sus hashes; este ajuste no modifica esos assets ni sus metadatos.

La verificación automática no prueba que una run completa tenga la dificultad deseada. Falta comparar en Play Mode un mismo encuentro con cada casco, incluyendo el jefe, y revisar daño recibido, tiempo de resolución y uso de consumibles. La elección final de números debe contrastarse con esas partidas.
