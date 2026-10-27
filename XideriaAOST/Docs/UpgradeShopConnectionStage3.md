# Conexión de la tienda del Lobby

La tienda instalada en `Canvas/UpgradeShop` se abre automáticamente cuando Sir Tete completa una ruta que termina sobre la casilla Shop existente del Lobby, `(6, 1)`. No hacen falta botones temporales ni referencias manuales nuevas.

Se agregó `UpgradeShopInteraction` al objeto `InputHandler` del Lobby, con referencias al jugador, GridSystem, GridInputHandler, GameStateManager y UpgradeShopUI ya presentes. No se modificó el prefab visual de la tienda ni su costo de prueba de 3 fragmentos.

## Apertura

`CharacterMovement.MovementCompleted` se emite al terminar todos los pasos solicitados y después del bloque que detiene la animación y ajusta la posición final. Los caminos que salen por cancelación, bloqueo, muerte o transición mediante `yield break` no emiten esa señal. Las puertas siguen comprobándose durante los pasos, como antes; solo se eliminó el mensaje provisional de Shop.

La interacción se suscribe únicamente al movimiento del jugador configurado. Revisa todas las casillas devueltas por `GetOccupiedCells`, sin confundir el origen lógico con el centro visual. Atravesar Shop y terminar en otra casilla no abre la tienda. Un personaje jugador de 2x2 puede activarla si su superficie final cubre Shop; los enemigos no la abren.

Después de la señal se espera al menos un fotograma y a que termine la acción del input, incluida una posible recogida de casco. Antes de abrir se comprueba otra vez que el jugador siga en la posición final, esté vivo, que el Lobby esté en exploración, que no haya transición, acción ni inventario abierto y que siga siendo el turno del jugador. No se abre por iniciar la escena sobre Shop ni por comprobar continuamente la posición. Para volver a abrir después de cerrar hay que completar otro movimiento que termine en Shop.

## Bloqueo y cierre

`UpgradeShopUI.OpenStateChanged` comunica apertura y cierre a la interacción. Mientras está abierta, `GridInputHandler` bloquea sus entradas del tablero y los puntos de entrada de habilidades y fin de turno desde el HUD. El inventario no puede abrirse encima. Se limpian las previsualizaciones; al cerrar se habilitan los controles y el refresco normal reconstruye las vistas necesarias.

El fotograma en que se cierra también queda bloqueado, para que el clic en la X no provoque movimiento o una acción en el tablero. No se modificaron Escape, MainMenuUI ni la lógica del TurnManager. No se pausa el tiempo global.

Deshabilitar o destruir la interacción cancela aperturas pendientes, elimina suscripciones y libera el bloqueo que adquirió. Deshabilitar o destruir la tienda también notifica el cierre. Las compras continúan usando exclusivamente la transacción de PlayerSessionData; esta conexión no concede monedas, no cambia recompensas y no activa pasivas directamente.

## Validación

Las comprobaciones automáticas incluyen compilación de los scripts de ejecución y de Editor, revisión de GUID/referencias y del cambio acotado del Lobby, y regresión de la lógica existente de monedas, ofertas y compras.

`UpgradeShopInteractionTests` agrega pruebas de Editor sobre superficie final 1x1/2x2, inicio sobre Shop sin apertura, espera de la acción, apertura/bloqueo/cierre, clic de cierre, cambios de estado mientras se espera, limpieza de suscripciones, recorrido que atraviesa Shop y movimientos rechazados o interrumpidos. Las pruebas usan la interfaz instalada y objetos de una escena de previsualización descartable. Su compilación no equivale a ejecutarlas en Unity.

Pendiente de comprobar en Unity Test Runner → EditMode → UpgradeShopInteractionTests, y en Play Mode:

1. Abrir el Lobby actualizado y caminar hasta Shop `(6, 1)`: la tienda aparece al detenerse.
2. Trazar una ruta que atraviese Shop y termine más adelante: no se abre.
3. Con la tienda abierta, probar tablero, Q/W/E/R, botón de habilidad, espacio, fin de turno y Tab: no deben ejecutar acciones ni abrir inventario.
4. Comprar Impulso Vital: descontar una vez 3 fragmentos, conservar oro y mostrar Adquirida. Repetir clics no descuenta más.
5. Cerrar con X: ese clic no mueve al personaje; el siguiente movimiento funciona. Salir de Shop y volver abre una sola tienda.
6. Repetir después de volver al Lobby desde otra sala; verificar que las monedas y compras se conservan.

La importación y el comportamiento visual real requieren comprobación dentro del editor. Si el Lobby ya estaba abierto antes de recibir los cambios externos, cargar su versión actualizada conservando primero cualquier trabajo sin guardar.
