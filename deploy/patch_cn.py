# -*- coding: utf-8 -*-
"""
Patch VirtualBT UI strings to Chinese:
1) Source XAML/C#  -> natural Chinese (any length)
2) resources.pri / VirtualBT.dll -> same-length in-place UTF-16-LE patch (space-padded)
Never touch strings that are also ConverterParameter / enum names / property names.
"""
import re, struct, os, shutil

# ---- Translation map (English -> Chinese, natural) ----
TRANS = {
    # long phrases (safe: multi-word)
    'Will make over the air requests if necessary based on GattRobustCaching if supported.':
        '关闭时：如有必要，将根据GattRobustCaching支持情况发起无线请求。',
    'Close Bluetooth connections upon navigating to Discovery page.':
        '打开时：导航到扫描页面时自动断开蓝牙连接。',
    'Leave Bluetooth connections open while navigating the app.':
        '关闭时：在应用内浏览时保持蓝牙连接不断开。',
    'Peripheral mode is not supported on this device':
        '此设备不支持从机(外设)模式，无法模拟蓝牙键盘。',
    'Will use locally cached values where possible.':
        '打开时：尽可能使用本地缓存的值。',
    'Central role is not supported on this device':
        '此设备不支持主机(中心)模式，无法扫描设备。',
    'Unsupported presentation format - using hex':
        '不支持的显示格式 - 已改用十六进制显示',
    'Include service data in advertisement.':
        '在广播数据中包含本服务的数据。',
    'Back button in titlebar or taskbar':
        '返回按钮显示在标题栏或任务栏',
    'Number of service changed events:':
        '服务变更事件次数：',
    'Number of Advertisement Services:':
        '广播服务数量：',
    'Include service in advertisement.':
        '在广播中包含本服务的UUID。',
    'Selected Advertisement Beacon':
        '已选中的广播信标',
    ' - Characteristic Short UUID:':
        ' - 特征短UUID：',
    'Discoverable and Connectable':
        '开启广播：可被发现且可连接',
    'Use shell-drawn back button':
        '使用系统绘制的返回按钮',
    'List of subscribed clients:':
        '已连接的客户端列表：',
    'Error setting notification':
        '设置通知时出错',
    'Back button in page header':
        '返回按钮在页面标题栏',
    'Alert Notification Service':
        '提醒通知服务',
    'Use Windows notifications':
        '使用Windows系统通知',
    'BT 4.2 Secure Connection:':
        '蓝牙4.2安全连接：',
    'Advertise as connectable.':
        '以可连接方式广播。',
    'MikeTheTech / Microsoft':
        'MikeTheTech / Microsoft',
    'Quick Content Filter: ':
        '快速内容过滤：',
    'Selected Advertisement':
        '已选中的广播',
    'Continuous Enumeration':
        '持续扫描设备',
    'Publish advertisement.':
        '发布蓝牙广播。',
    'Advertisement Monitor':
        '广播监听',
    ' - User Description: ':
        ' - 用户描述：',
    'WriteWithoutResponse':
        '写入(无需响应)',
    'Advertisement Beacon':
        '广播信标',
    'Advertisement Type: ':
        '广播类型：',
    'Characteristics Page':
        '特征页',
    'Device Services Page':
        '设备服务页',
    'Characteristic Name:':
        '特征名称：',
    'Enumeration finished':
        '扫描完成',
    'Use Extended Format':
        '使用扩展格式',
    'Data Section Type: ':
        '数据段类型：',
    'Data Section Data: ':
        '数据段内容：',
    'Number of Services:':
        '服务数量：',
    'Total Device Count:':
        '设备总数：',
    'Preferred Tx Power':
        '首选发射功率',
    'Virtual Peripheral':
        '虚拟外设',
    'Write Transaction':
        '事务写入',
    'Device Connected:':
        '设备已连接：',
    'Close Connections':
        '关闭蓝牙连接',
    'Privacy statement':
        '隐私声明',
    'Discover and Pair':
        '扫描与配对设备',
    'Microsoft Service':
        '微软服务',
    'User Description':
        '用户描述',
    'Include Tx Power':
        '包含发射功率',
    'Descriptor Name:':
        '描述符名称：',
    'Virtual Keyboard':
        '虚拟蓝牙键盘',
    'Scan Response: ':
        '扫描响应：',
    'Section Count: ':
        '段数量：',
    'Use Light Theme':
        '使用浅色主题',
    'Address Type: ':
        '地址类型：',
    'Section Type: ':
        '段类型：',
    'Section Data: ':
        '段数据：',
    'Service Name: ':
        '服务名称：',
    'Service UUID: ':
        '服务UUID：',
    'Connectable: ':
        '可连接：',
    'Write Value: ':
        '写入值：',
    'Service Name:':
        '服务名称：',
    'Settings Page':
        '设置页面',
    'Apply Filter':
        '应用过滤',
    'Clear Filter':
        '清除过滤',
    'Event Type: ':
        '事件类型：',
    'Read Value: ':
        '读取值：',
    'Properties: ':
        '属性：',
    'BT Address: ':
        '蓝牙地址：',
    'Service Page':
        '服务页',
    'Disconnected':
        '未连接',
    'Advertising':
        '广播中',
    'Timestamp: ':
        '时间戳：',
    'Anonymous: ':
        '匿名：',
    'Scannable: ':
        '可扫描：',
    'Light theme':
        '浅色主题',
    'Use Caching':
        '使用缓存',
    'Add Service':
        '添加服务',
    'Add Beacon':
        '添加信标',
    'Directed: ':
        '定向：',
    'Read Again':
        '重新读取',
    'Dark theme':
        '深色主题',
    'Publishing':
        '发布中',
    'Anonymous':
        '匿名',
    'Address: ':
        '地址：',
    'Payload: ':
        '负载：',
    'TxPower: ':
        '发射功率：',
    'VirtualBT':
        'VirtualBT',
    'Connected':
        '已连接',
    'Indicate':
        '指示',
    'Settings':
        '设置',
    'Handle: ':
        '句柄：',
    'Discover':
        '扫描设备',
    'Filter: ':
        '过滤：',
    'Privacy':
        '隐私',
    'Payload':
        '负载',
    'Decimal':
        '十进制',
    'Value: ':
        '值：',
    'Notify':
        '通知',
    'Active':
        '主动',
    'String':
        '字符串',
    'Rssi: ':
        '信号：',
    'Value':
        '值',
    'Write':
        '写入',
    'About':
        '关于',
    'Start':
        '开始',
    'UTF16':
        'UTF16',
    'Read':
        '读取',
    'Stop':
        '停止',
    'None':
        '无',
    'UTF8':
        'UTF8',
    'Hex':
        '十六进制',
    ' - ':
        ' - ',
    'Off':
        '关',
    ' (':
        ' (',
    'On':
        '开',
    ': ':
        ': ',
    ')':
        ')',
    # C# dialog strings
    'Fatal Error':
        '严重错误',
    'Error':
        '错误',
    'Undisplayable UTF8 character':
        '无法显示的UTF8字符',
    'No permission to access device':
        '没有访问该设备的权限',
    'Connection error':
        '连接错误',
    'Exception':
        '异常',
    'Connection failures':
        '连接失败',
    'Pairing error':
        '配对失败',
    'Error getting access to Bluetooth adapter. Do you have a have bluetooth enabled?':
        '无法访问蓝牙适配器，请确认蓝牙已启用且驱动正常。',
    'Please wait...':
        '请稍候...',
}

# Strings that must NOT be patched in resources.pri (converter params / enum names / property names).
SKIP_BIN = {
    'Hex', 'Decimal', 'UTF8', 'UTF16', 'String', 'None', 'Active', 'Passive',
    'Value', 'Text', 'Name', 'Content', 'Header', 'On', 'Off', 'IsOn',
    'When', 'Otherwise', 'Read', 'Write', 'Start', 'Stop',
    'Notify', 'Indicate',
    'Publishing', 'Advertising',
}

# VirtualBT.dll is .NET-Native-compiled and also holds reflection/type-info strings.
# Only patch unique dialog strings there.
DLL_KEYS = {
    'Fatal Error',
    'Undisplayable UTF8 character',
    'No permission to access device',
    'Connection error',
    'Connection failures',
    'Pairing error',
    'Error getting access to Bluetooth adapter. Do you have a have bluetooth enabled?',
    'Please wait...',
}

def pad_cn(cn: str, length: int) -> str:
    if len(cn) > length:
        raise ValueError(f'cannot fit {cn!r} into {length} chars')
    return cn + ' ' * (length - len(cn))

def patch_binary(path, trans_bin, label):
    data = bytearray(open(path, 'rb').read())
    total = 0
    for en, cn in trans_bin.items():
        ln = len(en)
        if len(cn) != ln:
            continue  # safety
        pat = struct.pack('<I', ln) + en.encode('utf-16-le') + b'\x00\x00'
        rep = struct.pack('<I', ln) + cn.encode('utf-16-le') + b'\x00\x00'
        n = 0
        i = 0
        while True:
            j = data.find(pat, i)
            if j < 0:
                break
            data[j:j + len(pat)] = rep
            n += 1
            i = j + len(rep)
        if n:
            print(f'  [{label}] {en!r} -> {cn!r}  x{n}')
            total += n
    if total:
        shutil.copy2(path, path + '.bak')
        open(path, 'wb').write(data)
        print(f'  [{label}] wrote {path} ({total} replacements)')
    else:
        print(f'  [{label}] no replacements')
    return total

def patch_source_files():
    """Replace English UI strings in source XAML/C# with natural Chinese."""
    roots = ['D:/VirtualBT-main/BluetoothLEExplorer/BluetoothLEExplorer']
    # longest first so 'Service Name: ' wins over 'Service Name:'
    keys = sorted(TRANS.keys(), key=len, reverse=True)
    count = 0
    for root in roots:
        for dirpath, dirs, files in os.walk(root):
            if 'obj' in dirpath or 'bin' in dirpath:
                continue
            for f in files:
                if not (f.endswith('.xaml') or f.endswith('.cs')):
                    continue
                p = os.path.join(dirpath, f)
                text = open(p, encoding='utf-8-sig').read()
                orig = text
                for en in keys:
                    cn = TRANS[en]
                    if cn == en:
                        continue
                    # attribute values and x:String contents and C# literals
                    text = text.replace(f'"{en}"', f'"{cn}"')
                    text = text.replace(f'>{en}<', f'>{cn}<')
                    text = text.replace(f'"{en},', f'"{cn},')  # MessageDialog(msg, "Title")
                if text != orig:
                    open(p, 'w', encoding='utf-8-sig').write(text)
                    print(f'  [src] {os.path.relpath(p, root)}')
                    count += 1
    return count

def build_map(allowed=None, skip=()):
    out = {}
    for en, cn in TRANS.items():
        if en in skip:
            continue
        if allowed is not None and en not in allowed:
            continue
        if cn == en:
            continue
        try:
            out[en] = pad_cn(cn, len(en))
        except ValueError as e:
            print(f'  SKIP (too long): {en!r} ({len(en)}) -> {cn!r} ({len(cn)})')
    return out

def main():
    print('=== Build PRI map (same-length padded) ===')
    trans_pri = build_map(skip=SKIP_BIN)
    print(f'PRI entries: {len(trans_pri)}')
    print('=== Build DLL map (dialog strings only) ===')
    trans_dll = build_map(allowed=DLL_KEYS)
    print(f'DLL entries: {len(trans_dll)}')

    print('=== Patch resources.pri ===')
    patch_binary('D:/VirtualBT-main/deploy/AppX/resources.pri', trans_pri, 'PRI')
    print('=== Patch VirtualBT.dll ===')
    patch_binary('D:/VirtualBT-main/deploy/AppX/VirtualBT.dll', trans_dll, 'DLL')

    print('=== Patch source XAML/C# ===')
    n = patch_source_files()
    print(f'source files modified: {n}')
    print('DONE')

if __name__ == '__main__':
    main()
