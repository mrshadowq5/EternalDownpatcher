# EternalDownpatcher — Guía de uso

[English](../DOCUMENTATION.md) · [Русский](../ru/DOCUMENTATION-RU.md) · [Español](DOCUMENTATION-ES.md)

[EternalDownpatcher](../../README.md) · Steam

## Contenido

- [Elige un método](#start)
- [Cambiar una instalación](#existing)
- [Descargar una copia separada](#separate)
- [Acceso y opciones de descarga](#steam)
- [Restaurar archivos y pedir ayuda](#support)

<a id="start"></a>

## Elige un método

Al iniciar, se crea `DOWNPATCH_FILES` si falta y se conserva su contenido existente. Si falta DepotDownloader, una ventana de progreso lo instala desde GitHub antes de abrir la ventana principal. Puedes cancelar el inicio o reintentar una instalación fallida. Después se buscan actualizaciones de DepotDownloader en segundo plano. La ventana muestra las versiones, la fecha, las notas y un enlace a la publicación. **OK** descarga e instala la actualización. **Omitir** o cerrar la ventana pospone el recordatorio hasta el siguiente inicio; se repite hasta actualizar. La propuesta espera a que terminen las operaciones del juego. Si la comprobación falla, la versión instalada sigue disponible.

Selecciona directamente «Descargar todo» o «Aplicar parche». «Descargar todo» es el modo predeterminado; «Validar archivos» es una opción independiente para aplicar un parche.

### Copia separada · Descargar todo

Descarga todos los archivos de la versión elegida en una carpeta separada. La instalación seleccionada no cambia y no necesitas tener el juego instalado. Usa este modo para conservar tu versión actual o si el programa no puede detectarla.

### Instalación existente · Aplicar parche

Descarga los archivos necesarios para volver a una versión anterior reconocida y los aplica automáticamente. Selecciona la carpeta con `DOOMEternalx64vk.exe` y una versión de destino más antigua. Primero se guardan los archivos que se van a reemplazar.

> **Elige una carpeta de salida vacía**
>
> Usa una carpeta dedicada, como `DOWNPATCH_FILES`. Si contiene archivos, el programa pide permiso para eliminar todo su contenido antes de continuar. No guardes archivos personales allí ni selecciones la carpeta del juego, la propia carpeta del programa o la raíz de una unidad.

### Antes de empezar

Cierra DOOM Eternal y espera a que Steam termine sus actualizaciones. Necesitas espacio para los archivos descargados y, al modificar una instalación, para la copia de seguridad. También necesitas internet y una cuenta de Steam que tenga el juego.

<a id="existing"></a>

## Cambiar una instalación

Selecciona «Aplicar parche». Este modo modifica la carpeta del juego seleccionada.

### 1 · Selecciona el juego y la versión

Pulsa «Seleccionar carpeta de DOOM Eternal» y elige la carpeta con `DOOMEternalx64vk.exe`. Espera a la detección y selecciona una «Versión del juego» anterior. Si no se reconoce la instalación, selecciona «Descargar todo» para obtener una copia separada.

### 2 · Prepara la descarga

Introduce el login y la contraseña de Steam. Elige una «Carpeta de archivos» vacía y separada del juego; ninguna puede estar dentro de la otra. La carpeta predeterminada es `DOWNPATCH_FILES`. El guardado de acceso se configura en Configuración → Steam; «Validar archivos» está disponible en este modo.

### 3 · Inicia y confirma el acceso

Pulsa «Aplicar downgrade» y revisa las confirmaciones. DepotDownloader se prepara al iniciar el programa. Introduce el código de correo o del autenticador cuando aparezca la ventana de Steam Guard, o aprueba el acceso en la aplicación móvil si Steam lo solicita. Los ocho depots se descargan en un solo proceso con una sesión de Steam compartida.

### 4 · Espera a la aplicación automática

Tras las descargas, el programa comprueba el EXE descargado y que la versión de origen no haya cambiado. Crea una copia de seguridad y aplica los archivos a la instalación elegida. No necesitas copiar archivos manualmente. Espera al mensaje de finalización.

> **Conserva toda la copia de seguridad**
>
> Se crea dentro de la carpeta del juego con el nombre `EternalDownpatcher_Backup_…`. Guarda la carpeta completa para «Restaurar copia». Contiene los archivos reemplazados y un registro de los añadidos por el parche. No es una copia de tus partidas guardadas ni de tu perfil de Steam.

<a id="separate"></a>

## Descargar una copia separada

Selecciona «Descargar todo». Puedes elegir cualquier versión incluida en la lista del programa.

### 1 · Selecciona Descargar todo

El botón de inicio cambia a «Descargar». La selección de una instalación existente se desactiva porque no es necesaria. Elige la «Versión del juego» que quieras descargar.

### 2 · Elige una carpeta e inicia sesión

Selecciona una «Carpeta de descarga» vacía y separada, con espacio para todo el juego. Introduce tu login y contraseña de Steam. Pulsa «Descargar», revisa las confirmaciones y completa las solicitudes de Steam Guard.

### 3 · Espera al mensaje de éxito

El programa descarga los archivos de la versión elegida, comprueba el EXE y crea `steam_appid.txt` automáticamente. Los archivos permanecen en la «Carpeta de descarga» y no se copian a la instalación seleccionada.

### 4 · Inicia tu copia

`DOOMEternalx64vk.exe` se encuentra en la «Carpeta de descarga». Puedes añadirlo a Steam como un juego que no es de Steam. Mantén Steam abierto con la cuenta que tiene DOOM Eternal.

> **Usa una carpeta separada**
>
> No selecciones una instalación existente como «Carpeta de descarga». Si ya contiene archivos, confirmar el vaciado elimina su contenido. «Cancelar» detiene la operación actual; una descarga parcial todavía no está lista para jugar.

<a id="steam"></a>

## Acceso y opciones de descarga

El login y la contraseña de Steam son obligatorios para iniciar cualquiera de los dos modos.

### Login de Steam y Steam Guard

Usa el nombre de acceso de la cuenta, no el nombre público del perfil. DepotDownloader inicia la sesión. Si solicita un código por correo o del autenticador, se abre una ventana de Steam Guard. La aprobación móvil se realiza en la aplicación de Steam.

### Guardar el acceso · Configuración → Steam

En Configuración → Steam puedes elegir Recordar el login de Steam y Recordar la contraseña de Steam de forma independiente. El login se recuerda por defecto; guardar la contraseña está desactivado y requiere aceptar un aviso. Pulsa OK para aplicar las opciones. Los datos se guardan al confirmar Configuración o iniciar una descarga. Desactivar una opción y pulsar OK elimina su copia guardada; los campos ya introducidos siguen disponibles durante la sesión actual. La contraseña se cifra para tu cuenta de Windows, pero otros programas ejecutados con esa cuenta podrían acceder a ella. DepotDownloader puede conservar datos de sesión de Steam por separado.

### «Validar archivos»

Activa una comprobación adicional de archivos mediante DepotDownloader al modificar una instalación existente. Puede aumentar la duración. La casilla se desmarca y se desactiva en el modo «Descargar todo».

### Registro y cancelación

El registro muestra la descarga actual y los errores. Las contraseñas y los códigos introducidos de Steam Guard se ocultan, pero puede incluir tu login y rutas locales. Revísalo antes de compartirlo. «Cancelar» solicita detener la descarga o la aplicación del parche actual.

<a id="support"></a>

## Restaurar archivos y pedir ayuda

Conserva el mensaje de error y la carpeta completa de la copia de seguridad.

### Restaurar copia

Cierra el juego, selecciona la instalación que quieres restaurar y pulsa «Restaurar copia». Elige la carpeta `EternalDownpatcher_Backup_…` correspondiente y confirma el destino. Selecciona la carpeta exterior, no la subcarpeta files. Las copias actuales restauran archivos reemplazados y eliminan los añadidos por el parche. Las antiguas sin un manifiesto no pueden identificar automáticamente los archivos añadidos.

Conserva tanto `eternaldownpatcher-backup.json` como la subcarpeta `files`. La restauración sobrescribe los cambios realizados en esos archivos después del parche. Si aplicaste varios parches, restaura sus copias en orden inverso, empezando por la más reciente.

Si la aplicación del parche falla o se cancela, el programa intenta restaurar los archivos ya reemplazados. Si esa recuperación también falla, conserva la copia; el mensaje de error indica su ubicación.

### Versión no detectada o modificada

La detección compara el tamaño de `DOOMEternalx64vk.exe` con los datos de versiones. Un EXE nuevo o modificado puede no coincidir. Usa «Descargar todo» para una copia separada. Si Steam cambia la instalación durante la descarga, la aplicación del parche se detiene; vuelve a seleccionar la carpeta antes de reintentar.

### Fallo de descarga o acceso en espera

Lee los últimos mensajes del registro. Comprueba internet, los datos de la cuenta, el espacio libre y las solicitudes de Steam Guard. Para reintentar, usa una carpeta dedicada y lee el aviso de vaciado antes de confirmar.

### El juego se descargó pero tiene problemas

Una descarga correcta confirma las comprobaciones de archivos y EXE que realiza el programa. No garantiza que una versión antigua funcione con todos los controladores, mods o partidas guardadas. Al pedir ayuda, indica la versión del juego, la GPU, el controlador y los síntomas exactos.

### Contacto

Pulsa «Copiar registro» en la ventana principal y revísalo antes de compartirlo. Pide ayuda en el hilo `DE Downpatcher Support` de [Modern DOOM Speedrunning Discord](https://discord.gg/dtDa9VZ) o contacta con **mr.shadowq5** en Discord.

Proyecto: https://github.com/mrshadowq5/EternalDownpatcher
