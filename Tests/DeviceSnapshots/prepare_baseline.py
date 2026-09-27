"""Materialize the reviewed provider for a differential check, never modify the Git source."""
from pathlib import Path
import subprocess
root=Path(__file__).resolve().parents[2]
source=subprocess.check_output(['git','show','9f1df2962a5a1023199a8e6fec8827bd4968bd2c:Assets/Scripts/Idas3ControllerDevices.cs'],cwd=root,text=True)
(root/'Tests/DeviceSnapshots/BaselineControllerDevices.generated.cs').write_text(source.replace('Idas3ControllerDevices','BaselineControllerDevices'))
