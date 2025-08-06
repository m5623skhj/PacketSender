#pragma once

class ClientProxyFunc
{
public:
	static bool Start(const std::wstring& clientCoreOptionFile, const std::wstring& sessionGetterOptionFile);
	static void Stop();
	static bool IsConnected();
	static bool SendPacket(char* streamData, int streamSize);
};

extern "C"
{
	inline __declspec(dllexport) bool Start(const wchar_t* clientCoreOptionFile, const wchar_t* sessionGetterOptionFile)
	{
		return ClientProxyFunc::Start(clientCoreOptionFile, sessionGetterOptionFile);
	}

	inline __declspec(dllexport) void Stop()
	{
		ClientProxyFunc::Stop();
	}

	inline __declspec(dllexport) bool IsConnected()
	{
		return ClientProxyFunc::IsConnected();
	}

	inline __declspec(dllexport) bool SendPacket(char* streamData, const int streamSize)
	{
		return ClientProxyFunc::SendPacket(streamData, streamSize);
	}
}