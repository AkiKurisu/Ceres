# Flow troubleshooting

Diagnose the failing stage before changing code.

## Discovery and generation

1. Confirm the declaration and its assembly compile.
2. Confirm the consuming assembly references the required Ceres runtime assembly.
3. Inspect source-generator or ILPP diagnostics and generated output.
4. Refresh the owning registry or domain only through the supported Editor path.
5. Check player preservation when Editor discovery succeeds but a build fails.

## Graph behavior

- Missing search entry: inspect metadata and registry inputs.
- Wrong port shape: inspect runtime port declarations before node-view styling.
- Lost values after reload: inspect serialized names, generic type identity, and container ownership.
- Implementable event not firing: inspect generated/injected bridge and host lifecycle.
- Hot reload mismatch: compare the active graph identity, generated runtime identity, and variable transfer rather than forcing a second reload.

Do not mask a generator failure with handwritten duplicate generated code. After a compilation or domain reload, use a fresh bounded Editor status query and inspect the Console; do not assume connection recovery proves compilation success.
