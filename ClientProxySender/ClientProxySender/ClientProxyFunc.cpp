#include "PreCompile.h"
#include "ClientProxyFunc.h"
#include "Client.h"

bool ClientProxyFunc::Start(const std::wstring& clientCoreOptionFile, const std::wstring& sessionGetterOptionFile)
{
	return TestClient::GetInst().Start(clientCoreOptionFile, sessionGetterOptionFile);
}

void ClientProxyFunc::Stop()
{
	TestClient::GetInst().Stop();
}

bool ClientProxyFunc::IsConnected()
{
	return TestClient::GetInst().IsConnected();
}

bool ClientProxyFunc::SendPacket(char* streamData, const int streamSize)
{
	if (not TestClient::IsConnected())
	{
		return false;
	}

	TestClient::SendPacket(streamData, streamSize);
	return true;
}

bool ClientProxyFunc::GetStreamDataFromStoredPacket(char* outStreamData, int* outStreamSize, int inStreamMaxSize)
{
	if (not TestClient::IsConnected())
	{
		return false;
	}

	return TestClient::GetInst().GetStreamDataFromStoredPacket(outStreamData, outStreamSize, inStreamMaxSize);
}