# Aparición de enemigos por mapa

Cada escena de combate tiene un objeto raíz `EnemySpawner`. En el Inspector:

- `Grid`: grilla de esa misma escena.
- `Allowed Enemies`: prefabs de combate permitidos. Arrastrar el prefab raíz que contiene `Character`. Cada tipo válido tiene la misma probabilidad y puede repetirse.
- `Spawn Points`: un enemigo por entrada. `Grid Position` es el origen lógico del rectángulo ocupado; `Visual Offset` desplaza únicamente la presentación en coordenadas de mundo, igual que el offset de `Character`.

La cantidad de puntos determina la cantidad de enemigos. La selección se realiza nuevamente al cargar la escena. Cambiar los prefabs permitidos no cambia las posiciones. No colocar enemigos manuales encima de esos puntos.

## Configuración inicial

Level01–Level10 permiten `Larva`, `Larva_Fuego`, `Larva_Tierra` y `Larva_Aire`. Todos los puntos conservan el offset original `(-0.3, 0.1, -0.3)`.

| Mapa | Casillas originales conservadas | Cantidad |
| --- | --- | --- |
| Level01 | (11,6), (8,9) | 2 |
| Level02 | (11,6), (5,1) | 2 |
| Level03 | (9,9), (7,3) | 2 |
| Level04 | (11,6), (9,9), (8,2) | 3 |
| Level05 | (11,6), (11,7) | 2 |
| Level06 | (11,6), (8,9), (8,2) | 3 |
| Level07 | (9,9), (7,3) | 2 |
| Level08 | (11,6), (9,9), (8,2) | 3 |
| Level09 | (11,6), (11,7) | 2 |
| Level10 | (11,6), (5,1) | 2 |
| LevelBossZ1 | (6,11) | 1 boss 2×2 |

El mapa del boss permite únicamente `Jefe_Zona1/BossZone1Combat`, extraído del enemigo completo de la escena, con sus estadísticas, habilidades, modelo y referencias originales. Su offset sigue siendo `(-0.2, 0.5, -0.8)`. `BossZone1` sigue siendo el prefab visual anidado. Se conservaron los puntos de las dos larvas colocadas pero desactivadas de Level07 como dos apariciones activas, siguiendo la cantidad colocada original.

Inicio, Lobby y Tutorial no tenían larvas de veneno colocadas y conservan su configuración. Level06–Level10 ya existían fuera de la lista actual de escenas del build; esta tarea configura también esos mapas sin modificar esa lista.

## Inicialización y validación

El spawner ejecuta `Start` con orden -100, después de los `Awake` de la grilla y los personajes de escena y antes de `GameStateManager.Start`. `TurnManager` ya espera un frame antes de recoger los combatientes. Las habilidades y pasivas mantienen sus configuradores actuales.

Se instancia bajo un padre inactivo y se llama a `Character.ConfigureSpawn` antes de activar el enemigo. Así `Character.Awake` registra directamente la posición definitiva, sin ocupar transitoriamente la casilla por defecto del prefab. Los enemigos quedan como objetos raíz en la misma escena, sin heredar la escala del spawner. Volver a llamar a `SpawnEnemies` no duplica el grupo.

Antes de cada aparición se comprueba toda la superficie ocupada: límites, terreno, vacío, cascos en el suelo y otros personajes. Un prefab que no cabe se excluye de esa selección; si ninguno cabe, se informa el índice del punto en la consola y se omite ese punto. Para conservar la cantidad, todos los puntos deben ser válidos. El offset y las rotaciones visuales no modifican la ocupación lógica.

`EnemySpawnerTests` cubre configuración previa a Awake, ocupación 1×1/2×2, offsets, escala, selección restringida, llamadas repetidas y puntos inválidos. `EnemySpawnerSceneTests` comprueba las 11 escenas, sus posiciones, offsets y listas permitidas. Los tests EditMode inicializan explícitamente `Character.Awake`; no sustituyen una prueba del ciclo de vida real en Play Mode.

Validación realizada: compilación de código y tests con Roslyn y referencias del proyecto; auditoría estática de cantidades, terreno, referencias serializadas y conservación de los objetos ajenos al cambio. Pendiente ejecutar los tests en Unity y probar en Play Mode distintas cargas de los mapas, turnos, muerte del último enemigo, recompensa/victoria y el boss.
