#include <windows.h>

//=== Необходимые для Lua константы ============================================================================//
#define LUA_LIB
#define LUA_BUILD_AS_DLL

//=== Заголовочные файлы LUA ===================================================================================//
extern "C" {
#include "Lua\lauxlib.h"
#include "Lua\lua.h"
}

//=== Получает указатель на выделенную именованную память =====================================================//

// Имя для выделенной памяти
TCHAR Name[] = TEXT("CSharpCommand2");
// Создаст, или подключится к уже созданной памяти с таким именем
HANDLE hFileCSharpCommand = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, 3000, Name);

// Имя для выделенной памяти
TCHAR Name2[] = TEXT("QUIKCommand2");
// Создаст, или подключится к уже созданной памяти с таким именем
HANDLE hFileMapQUIKCommand = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, 3000, Name2);

//=== Стандартная точка входа для DLL ==========================================================================//
BOOL APIENTRY DllMain(HANDLE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved)
{
	return TRUE;
}

static int forLua_GiveCommand(lua_State *L) // Отправляет сообщения для C#
{
	// Если указатель на память получен
	if (hFileCSharpCommand)
	{
		// Получает доступ (представление) непосредственно к чтению/записи байт
		PBYTE pb = (PBYTE)(MapViewOfFile(hFileCSharpCommand, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, 3000));

		// Если доступ получен
		if (pb != NULL)
		{
			// Если запись пустая (либо первая отправка, либо C# очистил память, подтвердив получение)
			if (pb[0] == 0)
			{
				// Записывает текст сообщения в память
				const char *S = lua_tostring(L, 1);
				memcpy(pb, S, strlen(S));
			}
			// Закрывает представление
			UnmapViewOfFile(pb);
		}
	}
	return(0);
}

static int forLua_GetCommand(lua_State *L) // Отправляет команду C# в Quik
{
	//Если указатель на память получен
	if (hFileMapQUIKCommand)
	{
		//Получает доступ к байтам памяти
		PBYTE pb = (PBYTE)(MapViewOfFile(hFileMapQUIKCommand, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, 3000));

		//Если доступ к байтам памяти получен
		if (pb != NULL)
		{
			//Если память чиста
			if (pb[0] == 0)
			{
				//Записывает в Lua-стек пустую строку
				lua_pushstring(L, "");
			}
			else //Если в памяти есть команда
			{
				//Записывает в Lua-стек полученную команду
				lua_pushstring(L, (char*)(pb));
				//Стирает запись, чтобы повторно не выполнить команду
				for (int i = 0; i < 3000; i++) { pb[i] = '\0'; }
			}

			//Закрывает представление
			UnmapViewOfFile(pb);
		}
		else lua_pushstring(L, "");//Если доступ к байтам памяти не был получен, записывает в Lua-стек пустую строку
	}
	else //Указатель на память не был получен
	{
		//Записывает в Lua-стек пустую строку
		lua_pushstring(L, "");
	}
	//Функция возвращает записанное значение из Lua-стека (пустую строку, или полученную команду)
	return(1);
}

//=== Регистрация реализованных в dll функций, чтобы они стали "видимы" для Lua ================================//
static struct luaL_reg ls_lib[] = {
	{ "GiveCommand", forLua_GiveCommand },
	{ "GetCommand", forLua_GetCommand },
	{ NULL, NULL }
};

//=== Регистрация названия библиотеки, видимого в скрипте Lua ==================================================//
extern "C" LUALIB_API int luaopen_QluaCSharpConnector(lua_State *L) {
	luaL_openlib(L, "QluaCSharpConnector", ls_lib, 0);
	return 0;
}