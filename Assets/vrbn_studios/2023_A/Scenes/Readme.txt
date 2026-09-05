This package uses the old Unity Input Manager.

If you find that the 'Toggle light' button is not working and or
generates error messages when clicked, you can fix this by doing either of the following methods:

1. 	Make sure your project supports the old Unity Input manager
	by going to Edit -> Project Settings -> Player -> Active input handling
	and either set it to 'Input Manager (old)' or 'Both'.
or
2.	With the sample scene open you can select the Gameobject called 'EventSystem' in the Hierarchy and in the
	Component called 'Standalone Input Module' click the button
	'Replace with InputSystemUIInputModule' to replace the Component with the new version.