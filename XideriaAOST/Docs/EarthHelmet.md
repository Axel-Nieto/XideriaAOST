# Casco de tierra: Rock

`HelmetType.Rock = 3` se añade después de Fire y Water, conservando sus valores serializados. `HelmetFactory.CreateRockHelmet` construye sus cuatro habilidades y sus dos pasivas; `HelmetCatalog` aporta el modelo equipado y la referencia al modelo Rock usado por la R. El catálogo persistente de Lobby configura 3 unidades almacenables y 3 cargas de casco, como fuego y agua. La restricción de equipamiento del lobby también se aplica a Rock.

## Prefabs y presentación

- `Assets/Prefabs/HelmetPrefabs/Helmet_Rock.prefab`: Animator raíz, SphereCollider de radio 0.5, HelmetPickup de tipo Rock y HelmetPickupAnimator conectado al Animator.
- `Helmet_Rock_Equipped.prefab`: Animator raíz con el mismo controlador; Character desactiva sus animadores al equiparlo, como hace con los demás cascos.
- `Assets/Animation/Helmet_Animation/Rock/Helmet_Rock.controller`: estados Spawn, Idle y Pickup; trigger `IsDisol`. Los tres clips propios animan el hijo Visual. Idle gira y flota; Pickup reduce la escala y llama a `OnPickupAnimationFinished`. El objeto lógico raíz conserva su posición.

Ambos modelos conservan el FBX Helmet_Rock, el material Roca y sus GUID. La escala raíz sigue siendo 0.5. El inventario crea una casilla Rock a partir de una casilla existente si falta; se conserva el botón propio del clon y el GridLayoutGroup la distribuye. Su icono usa el casco genérico mientras no haya arte específico. El HUD usa un tema ocre. Las pasivas tienen un recuadro blanco con tooltip hasta disponer de sus imágenes. Los tooltips de habilidades y pasivas tienen un ancho máximo de 600 unidades UI, limitado al 90% del contenedor en pantallas pequeñas. El texto se distribuye en líneas y el fondo crece en altura conservando sus márgenes.

Rock figura en las tablas de drops de Tutorial, Lobby y Level01–Level10/LevelBossZ1. Level01 y Level02 incluyen un Helmet_Rock_Test en la casilla (3, 6), a dos casillas del inicio de Tete (1, 6). Para probarlo sin esperar un drop, recogerlo y equiparlo desde el inventario fuera de Lobby. Como los cascos anteriores, el flujo normal parte de Lobby para crear su catálogo persistente.

## Habilidades

| Tecla | Habilidad | Energía | CD | Comportamiento |
| --- | --- | --- | --- | --- |
| Q | Giro de Tierra | 2 | 0 | Área de distancia Manhattan 2 alrededor de toda la superficie ocupada. Daño físico default `max(1, Attack − Defense)` una vez por enemigo y empuje de una casilla para los supervivientes. Respeta inmunidades y activa Imparable al chocar. Gira visualmente sin cambiar de casilla. |
| W | Escudo de Roca | 5 | 3 | Tres cargas. Cada impacto positivo consume una y aporta 10% de reducción; no es un escudo inicial del 30%. Las restantes vencen al inicio del siguiente turno propio. |
| E | Excavar | 1 | 0 | Seleccionar una casilla Blocked cardinal adyacente. Cruza una barrera de una casilla de grosor si cabe el cuerpo completo justo del otro lado. No cruza personajes, cascos, vacío ni barreras gruesas; Root impide usarla. |
| R | Erupción de Tierra | 6 | 4 | Área Manhattan 5 alrededor del cuerpo: daño físico default y empuje 1. Después aparecen hasta 3 rocas aleatorias en casillas libres de esa área; duran hasta el segundo inicio de turno propio. |

Cada enemigo alcanzado por Q añade una acumulación independiente de **Coraza de Tierra**, con 10% de reducción y duración de dos turnos propios. Una Q posterior no renueva acumulaciones anteriores. Se suma la reducción de las acumulaciones y la carga de W consumida por ese impacto, hasta un máximo de 100%; el daño final se redondea hacia arriba como en la armadura existente. Coraza e Imparable se retiran al cambiar de casco.

**Imparable** impide empujes. Un empuje propio que queda bloqueado por terreno, ocupación o límite del tablero aplica 2 puntos físicos extra una vez, incluso si el desplazamiento fue parcial. No se aplica ese bonus por intentar desplazar a un objetivo inmune. Si ambos actores son inmunes, ninguno se desplaza; esto incluye Tete con Rock y el boss. La reflexión de Coloso contra atacantes móviles conserva su comportamiento.

## Terreno y reglas compartidas

`TemporaryRockField`, asociado a la grilla, guarda el tipo original de cada casilla y lo restaura al vencer la roca. Puede cubrir Ground, Water o Lava libres; nunca personajes, cascos, obstáculos originales, vacío, puertas o shop. Usa el mismo Rock.fbx que las referencias del lobby, escalado uniformemente para ocupar el 80% del ancho de una casilla. La base visual se coloca 0.3 unidades por encima del plano lógico mediante `visualSurfaceOffset`, para sobresalir del piso decorativo de Zona 1. Estos ajustes visuales no cambian su casilla lógica. Cada roca bloquea realmente el terreno consultado por rutas, IA y movimiento. No hay dos rocas superpuestas; un cambio externo posterior de terreno se respeta al limpiar. La muerte del dueño y la desactivación de la grilla también limpian sus rocas.

Los alcances, áreas y destinos comprueban rectángulos ocupados completos, incluidos cuerpos 2×2. Excavar comprueba todo el recorrido barrido y teletransporta el origen lógico sólo después de validar; consume una única energía. La previsualización usa una sobrecarga con Character/Grid para representar esas superficies. Las habilidades previas delegan en su previsualización habitual. La API de simulación de IA que recibe un Character como objetivo no anuncia Excavar como un ataque; las simulaciones de una casilla bloqueada usan `CanUseFromCell`.

Después de una habilidad que cambie la posición, `GridInputHandler` intenta recoger un casco cardinalmente adyacente al cuerpo completo del jugador, con la misma rutina del movimiento normal. Respeta la capacidad del inventario y omite cascos ya recogidos. No intenta recoger si la habilidad falla, el jugador muere o no cambia de casilla.

## Validación

Compilación de todo el código de juego y los tests con las referencias del proyecto. Generación y muestreo de las animaciones en Unity 6000.3.9f1. Ejecución en ese Editor, en un proyecto temporal aislado, de pruebas NUnit de Rock, componentes de ambos prefabs, regresiones de larvas y cascos. Las copias de prueba usan los mismos fuentes compilados y referencias temporales a sus scripts precompilados; los assets del proyecto principal mantienen sus referencias a los `.cs` originales.

La auditoría del proyecto principal comprueba GUID completos, referencias locales, catálogo/drops y conservación de los objetos ajenos al cambio. Queda la comprobación visual en Play Mode del encaje del casco, giro de Tete, recogida, rocas con la iluminación real y sensación de los valores iniciales confirmados.
