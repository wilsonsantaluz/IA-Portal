"""Isolated NVIDIA CUDA driver smoke: one float addition, no models or host mounts."""
import ctypes as C
import json

cuda = C.CDLL('libcuda.so.1')

def call(name, argtypes, *args):
    function = getattr(cuda, name)
    function.argtypes = argtypes
    function.restype = C.c_int
    result = function(*args)
    if result:
        raise RuntimeError(f'{name} failed with CUDA status {result}; no GPU success claimed')

call('cuInit', [C.c_uint], 0)
count = C.c_int()
call('cuDeviceGetCount', [C.POINTER(C.c_int)], C.byref(count))
if count.value < 1:
    raise RuntimeError('No CUDA device exposed')
device = C.c_int()
call('cuDeviceGet', [C.POINTER(C.c_int), C.c_int], C.byref(device), 0)
name = C.create_string_buffer(128)
total = C.c_size_t()
major, minor = C.c_int(), C.c_int()
call('cuDeviceGetName', [C.c_void_p, C.c_int, C.c_int], name, len(name), device)
call('cuDeviceTotalMem_v2', [C.POINTER(C.c_size_t), C.c_int], C.byref(total), device)
call('cuDeviceComputeCapability', [C.POINTER(C.c_int), C.POINTER(C.c_int), C.c_int], C.byref(major), C.byref(minor), device)
if (major.value, minor.value) < (7, 5):
    raise RuntimeError('This probe targets SM7.5+; choose a compatible PTX target for an older GPU')

context, module = C.c_void_p(), C.c_void_p()
memory = C.c_uint64()
call('cuCtxCreate_v2', [C.POINTER(C.c_void_p), C.c_uint, C.c_int], C.byref(context), 0, device)
try:
    ptx = b'''.version 6.4
.target sm_75
.address_size 64
.visible .entry add_one(.param .u64 address) {
    .reg .b64 %ptr;
    .reg .f32 %value;
    ld.param.u64 %ptr, [address];
    ld.global.f32 %value, [%ptr];
    add.f32 %value, %value, 0f3F800000;
    st.global.f32 [%ptr], %value;
    ret;
}
'''
    call('cuModuleLoadData', [C.POINTER(C.c_void_p), C.c_char_p], C.byref(module), ptx)
    kernel = C.c_void_p()
    call('cuModuleGetFunction', [C.POINTER(C.c_void_p), C.c_void_p, C.c_char_p], C.byref(kernel), module, b'add_one')
    call('cuMemAlloc_v2', [C.POINTER(C.c_uint64), C.c_size_t], C.byref(memory), C.sizeof(C.c_float))
    try:
        value = C.c_float(41)
        call('cuMemcpyHtoD_v2', [C.c_uint64, C.c_void_p, C.c_size_t], memory, C.byref(value), C.sizeof(value))
        parameters = (C.c_void_p * 1)(C.cast(C.byref(memory), C.c_void_p))
        call('cuLaunchKernel', [C.c_void_p] + [C.c_uint] * 7 + [C.c_void_p, C.POINTER(C.c_void_p), C.c_void_p],
             kernel, 1, 1, 1, 1, 1, 1, 0, None, parameters, None)
        call('cuCtxSynchronize', [])
        call('cuMemcpyDtoH_v2', [C.c_void_p, C.c_uint64, C.c_size_t], C.byref(value), memory, C.sizeof(value))
        if value.value != 42:
            raise RuntimeError('GPU kernel result mismatch')
        print(json.dumps(dict(result='PASS', gpu=name.value.decode(), vramMiB=total.value//1048576,
                              computeCapability=f'{major.value}.{minor.value}', kernelResult=value.value)))
    finally:
        call('cuMemFree_v2', [C.c_uint64], memory)
finally:
    if module.value:
        call('cuModuleUnload', [C.c_void_p], module)
    call('cuCtxDestroy_v2', [C.c_void_p], context)
