"""Shared locations for reference inputs and disposable analysis output."""

import argparse
from datetime import datetime, timezone
from pathlib import Path
import uuid
import zipfile


REPOSITORY = Path(__file__).resolve().parents[3]
REFERENCE = REPOSITORY / 'Docs' / 'Reference' / 'DiceOfKalma'
WORK = REPOSITORY / 'Docs' / 'work'


def parser(description):
    result = argparse.ArgumentParser(description=description)
    result.add_argument('--input-dir', type=Path, default=REFERENCE,
                        help='Extracted reference snapshot or a previous analysis output directory.')
    result.add_argument('--apk-dir', type=Path, default=REFERENCE / 'Inputs',
                        help='Directory containing the preserved reference APK inputs.')
    result.add_argument('--output-dir', type=Path,
                        help='Empty output directory under Docs/work; a unique directory is the default.')
    return result


def output_directory(args, name):
    if args.output_dir is None:
        stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
        result = WORK / 'reference-analysis' / (name + '-' + stamp + '-' + uuid.uuid4().hex[:8])
    else:
        result = args.output_dir.resolve()
    if not result.is_relative_to(WORK.resolve()):
        raise ValueError('Analysis output must remain under Docs/work: ' + str(result))
    if result.exists() and any(result.iterdir()):
        raise FileExistsError('Output directory must be empty: ' + str(result))
    result.mkdir(parents=True, exist_ok=True)
    print('Output: ' + str(result))
    return result


def metadata(apk_dir):
    with zipfile.ZipFile(apk_dir / 'base.apk') as apk:
        return apk.read('assets/bin/Data/Managed/Metadata/global-metadata.dat')


def binary(apk_dir):
    with zipfile.ZipFile(apk_dir / 'split_config.arm64_v8a.apk') as apk:
        names = [name for name in apk.namelist() if name.endswith('/libil2cpp.so')]
        if len(names) != 1:
            raise ValueError('Expected exactly one libil2cpp.so in the ARM64 split APK.')
        return apk.read(names[0])
