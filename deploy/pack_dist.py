# -*- coding: utf-8 -*-
"""Assemble the green-zip release bundle for VirtualBT-CN.

Copies the .NET Native build output into deploy/AppX and deploy/dist/<name>,
stripping *.pdb / *.g.cs, then zips the folder with Python's zipfile so the
Chinese filenames keep their UTF-8 name flag (Compress-Archive garbles them).

Usage: python deploy/pack_dist.py 1.2.0
"""
import os
import shutil
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ILC = os.path.join(ROOT, r'BluetoothLEExplorer\BluetoothLEExplorer\bin\x86\Release\ilc')
APPX = os.path.join(ROOT, 'deploy', 'AppX')

STRIP_SUFFIXES = ('.pdb', '.g.cs')


def ignore_stripped(_dir, names):
    return [n for n in names if n.endswith(STRIP_SUFFIXES)]


def clean_copy(src, dst):
    if os.path.isdir(dst):
        shutil.rmtree(dst)
    shutil.copytree(src, dst, ignore=ignore_stripped)


def main():
    version = sys.argv[1] if len(sys.argv) > 1 else '1.2.0'
    name = 'VirtualBT-CN-v' + version
    dist = os.path.join(ROOT, 'deploy', 'dist', name)
    # Previous bundle: source of the runtime appx and the ASCII launcher.
    prev = os.path.join(ROOT, 'deploy', 'dist', 'VirtualBT-CN-v1.1.0')
    zip_path = os.path.join(ROOT, 'deploy', 'dist', name + '-win-x86.zip')

    if not os.path.isdir(ILC):
        sys.exit('Build output not found: ' + ILC)

    clean_copy(ILC, APPX)
    print('deploy/AppX refreshed:', len(os.listdir(APPX)), 'top-level entries')

    if os.path.isdir(dist):
        shutil.rmtree(dist)
    os.makedirs(dist)

    for f in ['CHANGELOG.md', 'LICENSE', 'NOTICE', 'Quick-Start.md', 'README.md',
              'README.zh-CN.md', 'launch-virtualbt.ps1', '启动VirtualBT.bat', '快速上手.md']:
        shutil.copy2(os.path.join(ROOT, f), os.path.join(dist, f))

    shutil.copy2(os.path.join(prev, 'VirtualBT-Launcher.bat'),
                 os.path.join(dist, 'VirtualBT-Launcher.bat'))

    os.makedirs(os.path.join(dist, 'deploy'))
    clean_copy(APPX, os.path.join(dist, 'deploy', 'AppX'))
    shutil.copytree(os.path.join(prev, 'deploy', 'nuget'),
                    os.path.join(dist, 'deploy', 'nuget'))

    if os.path.exists(zip_path):
        os.remove(zip_path)
    count = 0
    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as z:
        for root, _dirs, files in os.walk(dist):
            for f in files:
                p = os.path.join(root, f)
                arc = os.path.relpath(p, os.path.dirname(dist)).replace(os.sep, '/')
                z.write(p, arc)
                count += 1
    print('dist folder files:', count)
    print('zip:', zip_path, round(os.path.getsize(zip_path) / 1024 / 1024, 1), 'MB')


if __name__ == '__main__':
    main()
