# How to build locally

- Open a developer command prompt
- Launch `build.cmd`

### Optional - install pdbstr.exe

The build process use [SourceLink](http://ctaggart.github.io/SourceLink/index.html) to allow source stepping using the git repo as a source server.
SourceLink require `pdbstr.exe` to be able to instrument the pdb files.
To obtain `pdbstr.exe` you can either

- Install Debugging Tools for Windows that is a part of the Windows 8.1 SDK. 
  That SDK is preinstalled on build services like AppVeyor. 
- Install SourceLink using chocolatey **[Recommended]**
	- Tip: In the `build\tools\external` launch the `install_pdbstr(through chocolatey).cmd` script. 
	  It will install chocolatey and then the SourceLink package that contain a stripped down `pdbstr.exe`.
	  Note that the script require administrative privileges.




