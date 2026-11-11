"""Windows DPAPI encryption for transient mobile card state."""
import base64
import ctypes
import os
from ctypes import wintypes

if os.name != "nt":
    raise RuntimeError("Windows DPAPI is required")

class DataBlob(ctypes.Structure):
    _fields_ = [("size", wintypes.DWORD), ("data", ctypes.POINTER(ctypes.c_byte))]

crypt32 = ctypes.WinDLL("crypt32", use_last_error=True)
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
crypt32.CryptProtectData.argtypes = [
    ctypes.POINTER(DataBlob), wintypes.LPCWSTR, ctypes.POINTER(DataBlob),
    ctypes.c_void_p, ctypes.c_void_p, wintypes.DWORD, ctypes.POINTER(DataBlob)]
crypt32.CryptProtectData.restype = wintypes.BOOL
crypt32.CryptUnprotectData.argtypes = [
    ctypes.POINTER(DataBlob), ctypes.c_void_p, ctypes.POINTER(DataBlob),
    ctypes.c_void_p, ctypes.c_void_p, wintypes.DWORD, ctypes.POINTER(DataBlob)]
crypt32.CryptUnprotectData.restype = wintypes.BOOL
kernel32.LocalFree.argtypes = [ctypes.c_void_p]
kernel32.LocalFree.restype = ctypes.c_void_p

def _convert(data, encrypt):
    buf = ctypes.create_string_buffer(data)
    source = DataBlob(len(data), ctypes.cast(buf, ctypes.POINTER(ctypes.c_byte)))
    output = DataBlob()
    fn = crypt32.CryptProtectData if encrypt else crypt32.CryptUnprotectData
    if encrypt:
        ok = fn(ctypes.byref(source), "OpenFill mobile bridge",
                None, None, None, 1, ctypes.byref(output))
    else:
        ok = fn(ctypes.byref(source), None,
                None, None, None, 1, ctypes.byref(output))
    if not ok:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        return ctypes.string_at(output.data, output.size)
    finally:
        kernel32.LocalFree(ctypes.cast(output.data, ctypes.c_void_p))

def protect(value):
    if not isinstance(value, str):
        raise TypeError("Expected text")
    return base64.b64encode(_convert(value.encode("utf-8"), True)).decode("ascii")

def unprotect(value):
    return _convert(base64.b64decode(value, validate=True), False).decode("utf-8")
