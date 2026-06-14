FROM mono:6.12

WORKDIR /src
COPY . .

# Make sure intermediate folder exists for BuildVersion.cs generation
RUN mkdir -p BK7231Flasher/obj/Release

# Get nuget.exe (no apt needed)
RUN curl -fsSL -o /tmp/nuget.exe https://dist.nuget.org/win-x86-commandline/latest/nuget.exe

# Download required packages into /pkgs
# Pin versions that have net472 assets and include JsonObject (Nodes is inside System.Text.Json 6+)
RUN mkdir -p /pkgs && \
    mono /tmp/nuget.exe install System.Text.Json -Version 6.0.0 -OutputDirectory /pkgs -ExcludeVersion && \
    mono /tmp/nuget.exe install System.Memory -Version 4.5.5 -OutputDirectory /pkgs -ExcludeVersion && \
    mono /tmp/nuget.exe install System.Buffers -Version 4.5.1 -OutputDirectory /pkgs -ExcludeVersion && \
    mono /tmp/nuget.exe install System.Runtime.CompilerServices.Unsafe -Version 6.0.0 -OutputDirectory /pkgs -ExcludeVersion && \
    mono /tmp/nuget.exe install System.Threading.Tasks.Extensions -Version 4.5.4 -OutputDirectory /pkgs -ExcludeVersion

# Create a reference folder and copy the best-match DLLs (prefer net472, fallback netstandard2.0)
RUN mkdir -p /refs && \
    (cp -f /pkgs/System.Text.Json/lib/net472/*.dll /refs/ 2>/dev/null || cp -f /pkgs/System.Text.Json/lib/netstandard2.0/*.dll /refs/) && \
    (cp -f /pkgs/System.Memory/lib/net472/*.dll /refs/ 2>/dev/null || cp -f /pkgs/System.Memory/lib/netstandard2.0/*.dll /refs/) && \
    (cp -f /pkgs/System.Buffers/lib/net472/*.dll /refs/ 2>/dev/null || cp -f /pkgs/System.Buffers/lib/netstandard2.0/*.dll /refs/) && \
    (cp -f /pkgs/System.Runtime.CompilerServices.Unsafe/lib/net472/*.dll /refs/ 2>/dev/null || cp -f /pkgs/System.Runtime.CompilerServices.Unsafe/lib/netstandard2.0/*.dll /refs/) && \
    (cp -f /pkgs/System.Threading.Tasks.Extensions/lib/net472/*.dll /refs/ 2>/dev/null || cp -f /pkgs/System.Threading.Tasks.Extensions/lib/netstandard2.0/*.dll /refs/) && \
    ls -la /refs

# Build with extra reference search path
RUN msbuild BK7231Flasher.sln /p:Configuration=Release /m /p:ReferencePath=/refs

# Collect output
RUN mkdir -p /out && cp -r BK7231Flasher/bin/Release/* /out/
