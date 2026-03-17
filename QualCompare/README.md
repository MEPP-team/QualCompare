
# How to install opencv on the current blender python version

## Windows

Open a terminal with admin rights and type the following commands :

```bash
cd "C:\Program Files\Blender Foundation\Blender X.Y\X.Y\python\bin"
.\python.exe -m pip install --upgrade --force-reinstall --no-cache-dir --target="C:\Program Files\Blender Foundation\Blender X.Y\X.Y\python\lib\site-packages" opencv-python
```

!!! don't forget to replace X.Y with your current blender version !!!

### If it didn't work

#### 1° Use the good python interpretor

##### 2) Open new Powershell with admin rights

##### 3) Locate yourself at the good place

e.g.

```bash
cd "C:\Program Files\Blender Foundation\Blender 4.4\4.4\python\bin"
./python.exe -m ensurepip
./python.exe -m pip install --upgrade pip
./python.exe -m pip install --force-reinstall opencv-python
```

#### 2° Use a temporary venv to download and copy

Install opencv-python in a custom temporary virtual environment and copy the files manually :

##### 1) Open new Powershell with admin rights

##### 2) Create temporary venv

```bash
python -m venv opencv_env
opencv_env\Scripts\activate
pip install opencv-python
```

> If you get the script activation disabled for this system error, run in the PowerShell:
>
> Set-ExecutionPolicy Unrestricted -Scope CurrentUser -Force

##### 3) Copy freshly installed package folders to the blender packages folder

```bash
cp -Path opencv_env\Lib\site-packages\ -Destination "C:\Program Files\Blender Foundation\Blender 4.4\4.4\python\lib\site-packages" -Recurse

```

##### 4) Finally, delete the venv



```bash
rm opencv_env
```
