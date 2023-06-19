# ESPHome for HomeSeer 4
This plugin provides some basic functionality for [ESPHome](https://esphome.io/) devices within HS4, using the [ESPHome Native API](https://esphome.io/components/api.html)
Currently, there is not support for the Noise encrpytion layer, so all devices must have it disabled.

Please note that this isn't a comprehensive library at the moment - it is mainly provided here for reference.

## Supported Components
### Lights
- RGB Color control, Brightness, and On/Off are supported
  - This needs to be extended to support different lighting configurations 
- Color temperature and Cold/Warm White control aren't implemented
- This probably will break with some configurations - I haven't tested this extensively
### Fans
- On/Off and various speed settings are supported
  - High/Low, High/Medium/Low, and 0-100 is implemented. Other speed settings are not.
  - No support for reversing yet.
### Switches
- Binary switches should show up as expected
### Buttons
- Buttons should show up as expected


## Installation
1. Download this repository, and open in visual studio.
2. Build the release version of the applicatio
3. Copy the `HSPI_ESPHomeNative.exe` and `HSPI_ESPHomeNative.exe.config` to your HS4 root directory
4. Copy the other DLL files to `/bin/ESPHomeNative`
5. Restart Homeseer

Once installed and enabled, the plugin will automatically use mDNS to discover EPSHome devices on the same network. 
