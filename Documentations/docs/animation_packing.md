# Animation Packing

Animation Packing stores text-serialized Unity `AnimationClip` assets as compact `.animbin` files.
Unity imports them as ordinary `AnimationClip` assets, so they can be used by Animator Controllers,
Animator Override Controllers, and AssetBundles without runtime integration.

Packing preserves the complete animation data. It does not reduce keys, quantize values, or change
animation quality.

## Pack an Animation

1. Select a text-serialized `.anim` asset.
2. Choose **Tools > Ceres > Animation > Pack**.
3. Select the destination for the `.animbin` asset.

The new asset can be assigned anywhere an `AnimationClip` is accepted.

## Extract an Editable Copy

1. Select an `.animbin` asset.
2. Choose **Tools > Ceres > Animation > Extract**.
3. Select the destination for the `.anim` copy.

Imported `.animbin` clips should be treated as read-only. Extract an editable copy before making
persistent animation changes.

## Editor API

The API is available in the `Ceres.AnimationPacking` namespace and Editor assembly:

```csharp
AnimationBinaryUtility.Pack(animationAssetPath, outputPath);
AnimationBinaryUtility.Extract(animationBinaryPath, outputAnimationPath);
```

Both methods fail when the destination already exists. `Pack` accepts only text-serialized `.anim`
assets, and `Extract` writes an ordinary `.anim` asset.
