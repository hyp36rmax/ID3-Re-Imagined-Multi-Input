# Reading build status

Routine push and pull-request checks validate portable code and Windows native builds. A green summary does not establish a playable Unity build or hardware acceptance.

When hosted Unity activation is unavailable, the summary reports **PLAYER BUILD BLOCKED** without failing otherwise successful routine checks. If Unity runs and fails, the workflow still fails. Portable or native failures also remain failures. Pull requests never receive activation credentials.

For a strict package check, manually run **Multi-input sample validation** on `ID3-Multi-device-input` and enable **Require complete Unity packages**. Missing activation fails that run, as does a failed Unity build. Successful packaging still requires rendered and hardware testing.

The current hosted route requires its configured eligible Unity activation. Activation on the owner's Windows PC does not configure the hosted runner. The owner-operated Windows build remains available. A self-hosted Windows runner has not been provisioned by this change; that requires connecting the machine and restricting execution to trusted reviewed code.
