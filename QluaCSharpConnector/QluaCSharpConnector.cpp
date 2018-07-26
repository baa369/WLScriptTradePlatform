#include <windows.h>

//=== ����������� ��� Lua ��������� ============================================================================//
#define LUA_LIB
#define LUA_BUILD_AS_DLL

//=== ������������ ����� LUA ===================================================================================//
extern "C" {
#include "Lua\lauxlib.h"
#include "Lua\lua.h"
}

//=== �������� ��������� �� ���������� ����������� ������ =====================================================//

// ��� ��� ���������� ������
TCHAR Name[] = TEXT("CSharpCommand2");
// �������, ��� ����������� � ��� ��������� ������ � ����� ������
HANDLE hFileCSharpCommand = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, 3000, Name);

// ��� ��� ���������� ������
TCHAR Name2[] = TEXT("QUIKCommand2");
// �������, ��� ����������� � ��� ��������� ������ � ����� ������
HANDLE hFileMapQUIKCommand = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, 3000, Name2);

//=== ����������� ����� ����� ��� DLL ==========================================================================//
BOOL APIENTRY DllMain(HANDLE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved)
{
	return TRUE;
}

static int forLua_GiveCommand(lua_State *L) // ���������� ��������� ��� C#
{
	// ���� ��������� �� ������ �������
	if (hFileCSharpCommand)
	{
		// �������� ������ (�������������) ��������������� � ������/������ ����
		PBYTE pb = (PBYTE)(MapViewOfFile(hFileCSharpCommand, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, 3000));

		// ���� ������ �������
		if (pb != NULL)
		{
			// ���� ������ ������ (���� ������ ��������, ���� C# ������� ������, ���������� ���������)
			if (pb[0] == 0)
			{
				// ���������� ����� ��������� � ������
				const char *S = lua_tostring(L, 1);
				memcpy(pb, S, strlen(S));
			}
			// ��������� �������������
			UnmapViewOfFile(pb);
		}
	}
	return(0);
}

static int forLua_GetCommand(lua_State *L) // ���������� ������� C# � Quik
{
	//���� ��������� �� ������ �������
	if (hFileMapQUIKCommand)
	{
		//�������� ������ � ������ ������
		PBYTE pb = (PBYTE)(MapViewOfFile(hFileMapQUIKCommand, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, 3000));

		//���� ������ � ������ ������ �������
		if (pb != NULL)
		{
			//���� ������ �����
			if (pb[0] == 0)
			{
				//���������� � Lua-���� ������ ������
				lua_pushstring(L, "");
			}
			else //���� � ������ ���� �������
			{
				//���������� � Lua-���� ���������� �������
				lua_pushstring(L, (char*)(pb));
				//������� ������, ����� �������� �� ��������� �������
				for (int i = 0; i < 3000; i++) { pb[i] = '\0'; }
			}

			//��������� �������������
			UnmapViewOfFile(pb);
		}
		else lua_pushstring(L, "");//���� ������ � ������ ������ �� ��� �������, ���������� � Lua-���� ������ ������
	}
	else //��������� �� ������ �� ��� �������
	{
		//���������� � Lua-���� ������ ������
		lua_pushstring(L, "");
	}
	//������� ���������� ���������� �������� �� Lua-����� (������ ������, ��� ���������� �������)
	return(1);
}

//=== ����������� ������������� � dll �������, ����� ��� ����� "������" ��� Lua ================================//
static struct luaL_reg ls_lib[] = {
	{ "GiveCommand", forLua_GiveCommand },
	{ "GetCommand", forLua_GetCommand },
	{ NULL, NULL }
};

//=== ����������� �������� ����������, �������� � ������� Lua ==================================================//
extern "C" LUALIB_API int luaopen_QluaCSharpConnector(lua_State *L) {
	luaL_openlib(L, "QluaCSharpConnector", ls_lib, 0);
	return 0;
}