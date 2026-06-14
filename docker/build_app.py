import os
import shutil
import subprocess
import sys
from pathlib import Path

IMAGE_TAG = "bk7231flasher-build"
CONTAINER_NAME = "bk7231flasher_build_tmp"
SOLUTION_NAME = "BK7231Flasher.sln"

def run(cmd, cwd=None):
    print(f"\n> {' '.join(map(str, cmd))}")
    subprocess.run(list(map(str, cmd)), cwd=str(cwd) if cwd else None, check=True)

def find_repo_root(start: Path) -> Path:
    p = start.resolve()
    for _ in range(25):
        if (p / SOLUTION_NAME).exists():
            return p
        if p.parent == p:
            break
        p = p.parent
    raise FileNotFoundError(f"Could not find {SOLUTION_NAME} walking up from {start}")

def copy_repo_to_context(repo_root: Path, ctx_root: Path, docker_dir: Path):
    """
    Copy the repo into a temp context dir to bypass upstream .dockerignore.
    Keep everything inside docker folder.
    """
    if ctx_root.exists():
        shutil.rmtree(ctx_root)
    ctx_root.mkdir(parents=True, exist_ok=True)

    # Basic excludes
    excludes = {
        ".git",
        ".github",
        "docker/release",
        "docker/_ctx",
        "docker/__pycache__",
        "__pycache__",
    }

    def ignore_func(dirpath, names):
        rel = Path(dirpath).resolve().relative_to(repo_root.resolve())
        rel_posix = rel.as_posix()

        ignored = set()
        for n in names:
            p = (rel / n).as_posix()

            # Exclude .git anywhere
            if n == ".git":
                ignored.add(n)
                continue

            # Exclude our docker artifacts
            if p.startswith("docker/release") or p.startswith("docker/_ctx") or p.endswith("__pycache__"):
                ignored.add(n)
                continue

        return ignored

    # Copy entire repo tree
    shutil.copytree(repo_root, ctx_root, dirs_exist_ok=True, ignore=ignore_func)

    # Ensure the docker folder inside context contains our Dockerfile + build scripts
    # (copytree above already copied them, but this makes sure)
    (ctx_root / "docker").mkdir(exist_ok=True)

def main():
    docker_dir = Path(__file__).resolve().parent
    repo_root = find_repo_root(docker_dir)

    dockerfile = docker_dir / "Dockerfile"
    if not dockerfile.exists():
        print(f"ERROR: Dockerfile not found: {dockerfile}")
        sys.exit(1)

    release_dir = docker_dir / "release"
    tmp_dir = release_dir / "_tmp_out"
    ctx_dir = docker_dir / "_ctx"

    release_dir.mkdir(exist_ok=True)
    # remove old exe(s)
    for old in release_dir.glob("*.exe"):
        old.unlink()
    if tmp_dir.exists():
        shutil.rmtree(tmp_dir)
    tmp_dir.mkdir(parents=True, exist_ok=True)

    # Clean leftover container
    subprocess.run(["docker", "rm", "-f", CONTAINER_NAME],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    # Create temp context (inside docker folder)
    print(f"\nPreparing build context in: {ctx_dir}")
    copy_repo_to_context(repo_root, ctx_dir, docker_dir)

    # Build with ctx_dir as context, and dockerfile path inside that context
    ctx_dockerfile = ctx_dir / "docker" / "Dockerfile"
    if not ctx_dockerfile.exists():
        # Copy our real Dockerfile into the context, just in case
        shutil.copy2(dockerfile, ctx_dockerfile)

    run(["docker", "build", "-f", str(ctx_dockerfile), "-t", IMAGE_TAG, str(ctx_dir)])

    # Create container and copy /out
    run(["docker", "create", "--name", CONTAINER_NAME, IMAGE_TAG])
    run(["docker", "cp", f"{CONTAINER_NAME}:/out/.", str(tmp_dir)])
    run(["docker", "rm", "-f", CONTAINER_NAME])

    # Pick exe
    exes = sorted(tmp_dir.rglob("*.exe"))
    if not exes:
        print("ERROR: No .exe found in container /out output.")
        sys.exit(2)

    preferred = None
    for e in exes:
        n = e.name.lower()
        if ("bk7231" in n) or ("flasher" in n) or ("gui" in n):
            preferred = e
            break
    exe_path = preferred or max(exes, key=lambda p: p.stat().st_size)

    final_exe = release_dir / exe_path.name
    shutil.copy2(exe_path, final_exe)

    # Cleanup: keep only exe in release, remove tmp + ctx
    shutil.rmtree(tmp_dir)
    shutil.rmtree(ctx_dir)

    print("\n✅ Done")
    print(f"Release EXE: {final_exe}")

if __name__ == "__main__":
    try:
        main()
    except FileNotFoundError as e:
        print(f"ERROR: {e}")
        sys.exit(1)
    except subprocess.CalledProcessError as e:
        print(f"\nERROR: command failed with exit code {e.returncode}")
        sys.exit(e.returncode)
