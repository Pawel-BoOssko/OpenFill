import ctypes
from ctypes import wintypes
import sys
pid=int(sys.argv[1]) if len(sys.argv)>1 else 2244
u=ctypes.windll.user32
CB=ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
windows=[]
def callback(hwnd, _):
    p=wintypes.DWORD()
    u.GetWindowThreadProcessId(hwnd,ctypes.byref(p))
    if p.value!=pid:return True
    title=ctypes.create_unicode_buffer(512)
    klass=ctypes.create_unicode_buffer(128)
    u.GetWindowTextW(hwnd,title,512)
    u.GetClassNameW(hwnd,klass,128)
    windows.append((hex(hwnd),bool(u.IsWindowVisible(hwnd)),klass.value,title.value))
    return True
u.EnumWindows(CB(callback),0)
for row in windows:print(row)
print("WINDOW_COUNT",len(windows))
