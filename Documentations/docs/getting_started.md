# Getting Started

Ceres requires Unity 2022.3 LTS or later.

## 1. Install NuGet dependencies

Add [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) through **Window > Package Manager > + > Add package from git URL**:

```text
https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
```

Open **NuGet > Manage NuGet Packages** and install `R3` version `1.3.0` and `MessagePack` version `3.1.4`.

## 2. Add UniTask

Add [UniTask](https://github.com/Cysharp/UniTask) through Package Manager using its Git URL:

```text
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.3
```

## 3. Add MessagePack for Unity

Add the Unity extension of [MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp) through Package Manager using:

```text
https://github.com/MessagePack-CSharp/MessagePack-CSharp.git?path=src/MessagePack.UnityClient/Assets/Scripts/MessagePack#v3.1.4
```

## 4. Add Ceres

Add Ceres through Package Manager using:

```text
https://github.com/AkiKurisu/Ceres.git?path=Packages/com.kurisu.ceres
```

Alternatively, add UniTask and Ceres to `Packages/manifest.json`:

```json
"dependencies": {
    "com.kurisu.ceres": "https://github.com/AkiKurisu/Ceres.git?path=Packages/com.kurisu.ceres",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.3"
}
```

Continue with [Ceres Architecture and Concepts](./ceres_concept.md) or [Flow Quick Startup](./flow_startup.md).
