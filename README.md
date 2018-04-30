# JavaApiDiff

Simple JAR diffing tool for added, removed, changed items

Compile: `msbuild JavaApiDiff.sln`

Usage: `mono JavaApiDiff.exe [--no-changed] jar-file-1 jar-file-2`

With the `--no-changed` flag, the tool will only display added/removed items between the two JARs
