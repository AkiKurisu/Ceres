# Capture

The Capture module renders a camera or copies the current screen into a
`Texture2D`, then optionally encodes and stores it as PNG. Use
`ScreenshotUtility` for direct texture ownership and `ScreenshotTool` for a
configured component with Gallery, Console, R3, and Flow integration.

## ScreenshotTool

`ScreenshotTool` is a sealed `FlowGraphObject`; attach it to a GameObject rather
than deriving from it.

```csharp
using Ceres.Gameplay.Capture;
using UnityEngine;

ScreenshotTool tool = gameObject.AddComponent<ScreenshotTool>();
tool.ScreenshotMode = ScreenshotMode.Camera;
tool.SourceCamera = Camera.main;
tool.SuperSize = 2;
tool.EnableHDR = false;
tool.DelayFrames = 1;
tool.MaxWarmupFrames = 32;
tool.TakeScreenshot();
```

Camera mode renders from `SourceCamera`, falling back to `Camera.main`, at the
Game view size multiplied by `SuperSize`. Screen mode captures the current
screen buffer and therefore includes rendered overlay UI. `SuperSize` affects
Camera mode only.

Camera capture uses an isolated hidden Camera copied from the source. Async
capture waits at least `DelayFrames` and stops waiting at `MaxWarmupFrames`;
render-pipeline integrations may report additional temporal warmup blockers.

The tool encodes the result as PNG, passes it to `GalleryUtility`, and raises
the static `OnScreenshotStart` and `OnScreenshotEnd` observables. The
`OnTakeScreenshotStart` and `OnTakeScreenshotEnd` methods are Flow
`ImplementableEvent` entry points, not C# virtual methods.

`GetLastScreenshot()` returns the tool-owned texture when it is still retained.
The next capture or component destruction releases it. In the Editor, the tool
releases the texture immediately after saving, so do not use this method as a
persistent asset store.

## Low-level Camera Capture

Use the synchronous API when an immediate render and CPU readback is acceptable:

```csharp
using Ceres.Gameplay.Capture;
using UnityEngine;

Texture2D image = ScreenshotUtility.CaptureRawScreenshot(
    Camera.main,
    new Vector2(1920, 1080),
    depthBuffer: 24,
    renderTextureFormat: RenderTextureFormat.ARGB32);

try
{
    byte[] png = image.EncodeToPNG();
}
finally
{
    Object.Destroy(image);
}
```

For an asynchronous GPU readback:

```csharp
using Ceres.Gameplay.Capture;
using Cysharp.Threading.Tasks;
using UnityEngine;

ScreenshotUtility.CaptureRawScreenshotAsync(
    Camera.main,
    new Vector2(3840, 2160),
    renderTextureFormat: RenderTextureFormat.ARGBHalf,
    delayFrames: 2,
    onComplete: image =>
    {
        GalleryUtility.SavePngToGallery(image.EncodeToPNG());
        Object.Destroy(image);
    },
    maxWarmupFrames: 32).Forget();
```

The callback owns the returned `Texture2D` and must destroy it. The synchronous
camera path uses the current quality anti-aliasing value; the asynchronous path
uses one sample. HDR half/float captures are converted from linear to gamma
before the texture is returned.

For an existing `RenderTexture`, `ToTexture2D` performs a synchronous readback
and `ToTexture2DAsync` uses `AsyncGPUReadback`.

## Screen Capture

`CaptureScreenShotFromScreen()` reads the active screen and must be called at
the end of a rendered frame. `CaptureScreenshotAsync(Action<Texture2D>)`
coordinates the asynchronous path and invokes the callback with a caller-owned
texture.

```csharp
using Ceres.Gameplay.Capture;
using Cysharp.Threading.Tasks;
using UnityEngine;

await UniTask.WaitForEndOfFrame();
Texture2D image = ScreenshotUtility.CaptureScreenShotFromScreen();
```

The request-level APIs accept a caller-owned destination `RenderTexture`:

```csharp
using Ceres.Gameplay.Capture;
using UnityEngine;

var request = new ScreenshotRequest
{
    Mode = ScreenshotMode.Camera,
    Camera = Camera.main,
    Destination = destination,
    DelayFrames = 1,
    MaxWarmupFrames = 32
};

ScreenshotUtility.ScreenshotHandler handler =
    await ScreenshotUtility.CaptureScreenshotAsync(request);

handler.Dispose();
```

The destination must exist before the call and remains owned by the caller.
Always dispose the returned handler; it owns temporary capture state, not the
destination texture.

## Gallery Output

`GalleryUtility.SavePngToGallery` accepts PNG bytes and an optional filename.
Without a filename it generates a timestamped `Capture-*.png` name.

- Android and iOS use the bundled native Gallery bridge and the album name
  `album`.
- The Editor and other platforms write below `GalleryUtility.SnapshotFolderPath`,
  a `Snapshots` directory beside `Application.dataPath`.

Mobile permission prompts and gallery writes are platform services. Saving is
not a transactional confirmation that the media is immediately visible to the
user.

## Console and Flow

The runtime Console command is:

```text
screenshot
screenshot false 2
```

The overload accepts `includeUI` and `superSize`. It creates a temporary
`ScreenshotTool`, captures once, and destroys the tool after the end event.

Flow exposes camera sync/async capture and screen sync/async capture under
**Gameplay/Capture**. A placed `ScreenshotTool` also exposes `TakeScreenshot`,
`GetLastScreenshot`, `OnTakeScreenshotStart`, and `OnTakeScreenshotEnd` through
its generated Flow API.

## Constraints

- Screen capture resolution is the current Game view or screen size; use Camera
  mode for an explicit output size.
- Camera mode captures what the copied Camera renders. Screen-space overlay UI
  requires Screen mode.
- Sync methods block for rendering/readback and should not be placed on a hot
  gameplay path.
- Async completion is still frame- and GPU-dependent; callbacks do not run at a
  deterministic wall-clock time.
- Ceres does not retain or dispose textures returned by the low-level utility.

Related API: <xref:Ceres.Gameplay.Capture.ScreenshotTool>,
<xref:Ceres.Gameplay.Capture.ScreenshotUtility>,
<xref:Ceres.Gameplay.Capture.ScreenshotRequest>, and
<xref:Ceres.Gameplay.Capture.GalleryUtility>.
