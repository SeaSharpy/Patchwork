dotnet build Conversions/Conversions.csproj -t:BuildConverter
rmdir /s /q Executables\Conversions
move Conversions/Executables/Conversions Executables/
Executables\Conversions\Conversions.exe