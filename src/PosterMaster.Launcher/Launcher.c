#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <strsafe.h>

static int ShowLaunchError(const wchar_t *message)
{
    MessageBoxW(NULL, message, L"Резак", MB_OK | MB_ICONERROR);
    return 1;
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE previousInstance, PWSTR arguments, int showCommand)
{
    (void)instance;
    (void)previousInstance;

    wchar_t dataDirectory[32768];
    DWORD length = GetModuleFileNameW(NULL, dataDirectory, ARRAYSIZE(dataDirectory));
    if (length == 0 || length >= ARRAYSIZE(dataDirectory)) {
        return ShowLaunchError(L"Не удалось определить расположение Rezak.exe.");
    }

    wchar_t *fileName = wcsrchr(dataDirectory, L'\\');
    if (fileName == NULL ||
        FAILED(StringCchCopyW(fileName + 1, ARRAYSIZE(dataDirectory) - (fileName + 1 - dataDirectory), L"Data"))) {
        return ShowLaunchError(L"Не удалось определить расположение папки Data.");
    }

    wchar_t application[32768];
    if (FAILED(StringCchPrintfW(application, ARRAYSIZE(application), L"%ls\\Rezak.exe", dataDirectory))) {
        return ShowLaunchError(L"Путь к приложению слишком длинный.");
    }

    wchar_t commandLine[32767];
    if (FAILED(StringCchPrintfW(commandLine, ARRAYSIZE(commandLine), L"\"%ls\" %ls", application, arguments))) {
        return ShowLaunchError(L"Командная строка слишком длинная.");
    }

    STARTUPINFOW startup = { 0 };
    startup.cb = sizeof(startup);
    startup.dwFlags = STARTF_USESHOWWINDOW;
    startup.wShowWindow = (WORD)showCommand;

    PROCESS_INFORMATION process;
    if (!CreateProcessW(application, commandLine, NULL, NULL, FALSE, CREATE_SUSPENDED, NULL, dataDirectory, &startup, &process)) {
        return ShowLaunchError(L"Не удалось запустить приложение. Проверьте, что папка Data со всеми файлами находится рядом с Rezak.exe.");
    }

    AllowSetForegroundWindow(process.dwProcessId);
    if (ResumeThread(process.hThread) == (DWORD)-1) {
        TerminateProcess(process.hProcess, 1);
        CloseHandle(process.hThread);
        CloseHandle(process.hProcess);
        return ShowLaunchError(L"Не удалось запустить приложение.");
    }

    CloseHandle(process.hThread);
    DWORD exitCode = 1;
    if (WaitForSingleObject(process.hProcess, INFINITE) == WAIT_OBJECT_0) {
        GetExitCodeProcess(process.hProcess, &exitCode);
    }
    CloseHandle(process.hProcess);
    return (int)exitCode;
}
