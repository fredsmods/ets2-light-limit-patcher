@echo off
rem SPDX-License-Identifier: MIT
rem ETS2 Light Limit Patcher - double-click to start.
rem Runs ETS2-Light-Patcher.ps1 with Windows PowerShell (built into Windows 10/11). Source: src\LightPatcher.cs
cd /d "%~dp0"
start "" /min powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File "%~dp0ETS2-Light-Patcher.ps1"
