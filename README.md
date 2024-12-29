# Ceres
Powerful node based visual scripting toolkit for Unity.

## Dependencies

Add following dependencies to `manifest.json`.

```json
  "dependencies": {
    "com.kurisu.chris": "https://github.com/AkiKurisu/Chris.git",
    "com.cysharp.unitask":"2.5.3",
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  }

```

## Platform

Unity 2022.3 LTS or later.

Support Mono, IL2CPP.

>Ceres relies on full generic sharing since Unity 2022 to support generic node in IL2CPP.

## Implementation

### Flow

Powerful visual scripting solution inspired from Unreal's Blueprint.
  
Include in this repository. 

See [Document](./Docs/flow_startup.md).

![ceres_flow](./Docs/Images/ceres_flow.png)
 
### Next Gen Dialogue

AI powered dialogue visual designer for Unity.

Is migrating to Ceres version, see [Next-Gen-Dialogue](https://github.com/AkiKurisu/Next-Gen-Dialogue/tree/ceres_main).

![ceres_ngd](./Docs/Images/ceres_ngd.png)

## Reference


[Chris](https://github.com/AkiKurisu/Chris) 

>Support Ceres to serialize any object and edit them in editor, 
also providing contextual event used in Flow.

[UniTask](https://github.com/Cysharp/UniTask) 

>Support Ceres to execute node in async.
