# Meshoptimizer plugins and bindless

## Rebuilding the DLL

Clone the `meshoptimer` repo:

```bash
git clone https://github.com/zeux/meshoptimizer.git
cd meshoptimizer
```

Build a DLL:

``` bash
del CMakeCache.txt
cmake . -DMESHOPT_BUILD_SHARED_LIBS=ON
cmake --build . --config Release 
```

The resulting DLL will be in `Release/meshoptimizer.dll`.
