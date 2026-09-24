// Purpose: Implements main responsibilities for crash watchdog.
#include <windows.h>
#include <dbghelp.h>
#include <filesystem>
#include <iostream>
#include <string>
#include <vector>
namespace fs=std::filesystem;
static std::wstring Quote(const std::wstring&s){if(s.find_first_of(L" \t\"")==std::wstring::npos)return s;std::wstring o=L"\"";unsigned slashes=0;for(auto c:s){if(c==L'\\'){slashes++;continue;}if(c==L'\"'){o.append(slashes*2+1,L'\\');o.push_back(c);slashes=0;continue;}o.append(slashes,L'\\');slashes=0;o.push_back(c);}o.append(slashes*2,L'\\');o.push_back(L'\"');return o;}
static std::wstring Get(int argc,wchar_t**argv,const wchar_t*key){for(int i=1;i+1<argc;i++)if(std::wstring(argv[i])==key)return argv[i+1];return L"";}
static int Sep(int argc,wchar_t**argv){for(int i=1;i<argc;i++)if(std::wstring(argv[i])==L"--")return i;return -1;}
static bool Dump(HANDLE process,DWORD pid,const fs::path&path,DWORD threadId,EXCEPTION_POINTERS* ep)
{
 fs::create_directories(path.parent_path());HANDLE file=CreateFileW(path.c_str(),GENERIC_WRITE,0,nullptr,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);if(file==INVALID_HANDLE_VALUE)return false;MINIDUMP_EXCEPTION_INFORMATION mei{threadId,ep,FALSE};auto type=(MINIDUMP_TYPE)(MiniDumpWithFullMemory|MiniDumpWithHandleData|MiniDumpWithThreadInfo|MiniDumpWithUnloadedModules);BOOL ok=MiniDumpWriteDump(process,pid,file,type,ep?&mei:nullptr,nullptr,nullptr);CloseHandle(file);return ok==TRUE;
}
int wmain(int argc,wchar_t**argv)
{
 auto dump=Get(argc,argv,L"--dump"),out=Get(argc,argv,L"--stdout"),err=Get(argc,argv,L"--stderr");int sep=Sep(argc,argv);if(dump.empty()||sep<0||sep+1>=argc){std::wcerr<<L"usage: --dump file [--stdout file --stderr file] -- executable args...\n";return 2;}
 fs::create_directories(fs::path(dump).parent_path());HANDLE hout=INVALID_HANDLE_VALUE,herr=INVALID_HANDLE_VALUE;if(!out.empty()){fs::create_directories(fs::path(out).parent_path());hout=CreateFileW(out.c_str(),GENERIC_WRITE,FILE_SHARE_READ,nullptr,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);}if(!err.empty()){fs::create_directories(fs::path(err).parent_path());herr=CreateFileW(err.c_str(),GENERIC_WRITE,FILE_SHARE_READ,nullptr,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);}
 std::wstring cmd;for(int i=sep+1;i<argc;i++){if(!cmd.empty())cmd.push_back(L' ');cmd+=Quote(argv[i]);}std::vector<wchar_t> mutableCmd(cmd.begin(),cmd.end());mutableCmd.push_back(L'\0');STARTUPINFOW si{};si.cb=sizeof(si);if(hout!=INVALID_HANDLE_VALUE||herr!=INVALID_HANDLE_VALUE){si.dwFlags|=STARTF_USESTDHANDLES;si.hStdOutput=hout==INVALID_HANDLE_VALUE?GetStdHandle(STD_OUTPUT_HANDLE):hout;si.hStdError=herr==INVALID_HANDLE_VALUE?GetStdHandle(STD_ERROR_HANDLE):herr;si.hStdInput=GetStdHandle(STD_INPUT_HANDLE);}PROCESS_INFORMATION pi{};
 if(!CreateProcessW(nullptr,mutableCmd.data(),nullptr,nullptr,TRUE,DEBUG_ONLY_THIS_PROCESS|CREATE_UNICODE_ENVIRONMENT,nullptr,nullptr,&si,&pi)){std::wcerr<<L"CreateProcess failed "<<GetLastError()<<L"\n";return 3;}CloseHandle(pi.hThread);DWORD exitCode=1;bool dumped=false;
 for(;;){DEBUG_EVENT ev{};if(!WaitForDebugEvent(&ev,INFINITE))break;DWORD cont=DBG_CONTINUE;if(ev.dwDebugEventCode==CREATE_PROCESS_DEBUG_EVENT){if(ev.u.CreateProcessInfo.hFile)CloseHandle(ev.u.CreateProcessInfo.hFile);}else if(ev.dwDebugEventCode==LOAD_DLL_DEBUG_EVENT){if(ev.u.LoadDll.hFile)CloseHandle(ev.u.LoadDll.hFile);}else if(ev.dwDebugEventCode==EXCEPTION_DEBUG_EVENT){auto& ex=ev.u.Exception;if(!ex.dwFirstChance&&!dumped){Dump(pi.hProcess,pi.dwProcessId,dump,ev.dwThreadId,nullptr);dumped=true;}cont=DBG_EXCEPTION_NOT_HANDLED;}else if(ev.dwDebugEventCode==EXIT_PROCESS_DEBUG_EVENT){exitCode=ev.u.ExitProcess.dwExitCode;ContinueDebugEvent(ev.dwProcessId,ev.dwThreadId,DBG_CONTINUE);break;}ContinueDebugEvent(ev.dwProcessId,ev.dwThreadId,cont);}
 CloseHandle(pi.hProcess);if(hout!=INVALID_HANDLE_VALUE)CloseHandle(hout);if(herr!=INVALID_HANDLE_VALUE)CloseHandle(herr);return static_cast<int>(exitCode);
}
