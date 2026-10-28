<div align="center">

<img src="docs/assets/banner.svg" width="1120" alt="TabPilot — Your tabs. One shortcut away." />

### Encuentra la pestaña. Mantén la concentración.

[简体中文](README.md) · [English](README.en.md) · [日本語](README.ja.md) · [Español](README.es.md)

**[↓ Descargar para Windows](https://github.com/aqvello/TabPilot/releases/tag/v0.2.1)** · [Compilar el código](#build) · [Compatibilidad y límites](#limits)

Windows x64 · .NET Framework 4.8 · UI: 简体中文 / English · v0.2.1 Preview

</div>

Un **selector de pestañas nativo de Windows para Chrome, entre ventanas y monitores**. Abre el panel, busca un título y vuelve a la pestaña que necesitas. Sin extensiones de Chrome.

| | |
| :--- | :--- |
| **⌨ Un atajo para buscar** | Busca títulos, selecciona con ↑↓ y cambia con Enter. |
| **▦ Varios monitores** | Agrupación por monitor, DPI mixto y posición guardada. |
| **◉ Nativo y local** | UI Automation / Win32. Sin reiniciar ni reconfigurar Chrome. |

## Mira el flujo de uso

![TabPilot: three screens → search → select → right-screen target brought into focus](docs/assets/demo-en.gif)

Encuentra una pestaña sin revisar cada ventana. Abre TabPilot, busca Design System y pulsa Enter para ir a la pestaña de la pantalla derecha. Un borde naranja te indica qué ventana se ha activado.

La demostración usa páginas de ejemplo. El zoom permite ver mejor el resultado. [Sobre la demostración](docs/assets/README.md)

| Antes: tres pantallas, pestaña de destino en segundo plano a la derecha | Después: ventana de la derecha activada y ampliada |
| :---: | :---: |
| ![Antes: tres pantallas, pestaña de destino en segundo plano a la derecha](docs/assets/workflow-before-en.png) | ![Después: ventana de la derecha activada y ampliada](docs/assets/workflow-after-en.png) |

## Interfaz en chino e inglés

| Chino simplificado | English |
| :---: | :---: |
| ![Chino simplificado](docs/assets/tabs-zh.png) | ![English](docs/assets/tabs-en.png) |

<details>
<summary>Más capturas: búsqueda y monitores</summary>

| Búsqueda por título | Por monitor |
| :---: | :---: |
| ![Búsqueda por título](docs/assets/search-en.png) | ![Por monitor](docs/assets/monitors-en.png) |

</details>

## Empieza en un minuto

1. Descarga y extrae `TabPilot-Windows-x64.zip` desde la Release de vista previa.
2. Ejecuta `TabPilot.exe` y conserva su archivo `.config` en la misma carpeta.
3. Usa el atajo global registrado. El predeterminado es **Ctrl + Alt + Espacio**; si está ocupado, se prueban alternativas y se muestra la combinación activa en el panel.
4. Busca un título y haz clic en un resultado, o usa ↑↓ y Enter. Se activa la ventana y se resalta durante unos dos segundos. Esc o un clic fuera ocultan el panel en la bandeja.

Requiere Windows 10 1703+ x64 y .NET Framework 4.8. **v0.2.1 es una vista previa**, probada localmente en Windows 10 / Chrome 156. No se garantiza compatibilidad con todas las versiones.

## Elige el idioma

La aplicación admite **chino simplificado e inglés**. Usa **中文 / EN** en la cabecera o **Language / 语言** en la bandeja. El cambio es inmediato y se conserva al reiniciar. Los títulos de páginas y algunos diagnósticos de Windows o Chrome mantienen su idioma original. Japonés y español son traducciones del README, no idiomas adicionales de la interfaz.

## Atajos principales

| Acción | Atajo / control |
| :--- | :--- |
| Mostrar / ocultar | Atajo global registrado |
| Seleccionar / cambiar | ↑↓ / Enter |
| Ocultar / salir | Esc / Ctrl + Q |
| Actualizar | F5 / Ctrl + R |
| Todo / monitor / recientes | Ctrl + 1 / 2 / 3 |
| Guardar posición | Arrastrar el título superior |

<a id="limits"></a>

## Compatibilidad y límites

- **Las pestañas accesibles no son necesariamente todas las pestañas.** Los grupos contraídos, permisos o el proveedor pueden ocultar entradas. La interfaz indica que la lista no está verificada como completa.
- **Windows controla la activación de ventanas.** Los diálogos, niveles de permisos y cambios de foco pueden impedirla. Se verifican los resultados y se informa de los fallos.
- Los grupos contraídos complejos, Chrome elevado, escritorios virtuales y otros entornos no están plenamente probados.
- No se obtienen con fiabilidad las URL de todas las pestañas en segundo plano. Se muestra la ventana; Recent solo registra cambios exitosos realizados mediante TabPilot.

## Datos y privacidad

TabPilot no sube datos de pestañas ni lee perfiles, historial o archivos de sesión de Chrome. Su configuración y diagnósticos se guardan en una carpeta `data` escribible o en `%LOCALAPPDATA%\TabPilot`. La configuración usa sustitución atómica, copia de seguridad y reintentos. Los diagnósticos pueden contener títulos: revísalos antes de compartirlos. Los medios usan datos ficticios; consulta [cómo se generan](docs/assets/README.md).

<a id="build"></a>

## Compilar el código

Utiliza el compilador .NET Framework del sistema, sin .NET SDK ni descargas de NuGet:

```powershell
git clone https://github.com/aqvello/TabPilot.git
Set-Location TabPilot
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\bin\TabPilot.exe
```

## Pruebas e implementación

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\build-gui-checks.ps1
.\bin\GuiChecks.exe
.\bin\TabPilot.Cli.exe self-test
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\release-checks.ps1
```

8 comprobaciones del núcleo y 77 de regresión GUI/modelo, incluidas traducciones, avisos, búsqueda, títulos originales y compatibilidad de ajustes. Las pruebas reales con monitores y los fallos observados se documentan aparte; las demostraciones sintéticas no prueban compatibilidad con Chrome.

[Validación GUI](docs/GUI_VALIDATION.md) · [Núcleo](docs/VALIDATION.md) · [CLI](docs/CONSOLE_POC.md) · [Cambios](CHANGELOG.md)

Las pruebas nativas de integración GUI manipulan ventanas reales y pueden necesitar coordenadas y estados previos específicos del equipo. No se incluyen capturas originales, ajustes personales, listas de pestañas ni registros en el repositorio o paquete. Las notas técnicas detalladas están actualmente en chino.

[MIT License](LICENSE) · Copyright © 2026 aqvello
