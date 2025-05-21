# DicomScoToBto
DicomScoToBto is a utility to decode Hologic Breast Tomosynthesis Objects, that was created based on Dave Harvey's investigation results found [here](https://groups.google.com/g/comp.protocols.dicom/c/aMrgjrMtyVc).\
The utility is using the [fo-dicom](https://github.com/fo-dicom/fo-dicom) library for all DICOM-related functions.

### Build
![build main](https://github.com/PantelisGeorgiadis/DicomScoToBto/actions/workflows/build.yml/badge.svg?branch=main)

	cd Build
	.\Build.ps1
[Visual Studio 2019](https://visualstudio.microsoft.com/downloads/) or [Build Tools for Visual Studio 2019](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2019) is required.

### Use
	DicomScoToBto.exe -i input_file -o output_file
	
### Cross-Platform
The utility has been tested to work on Windows, Mac and Linux (Ubuntu 16.04). Mac and Linux support is provided through the [mono framework](https://www.mono-project.com).

### License
DicomScoToBto is released under the MIT License.
