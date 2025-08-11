#include "PreCompile.h"
#include "Client.h"
#include "RUDPClientCore.h"
#include "Logger.h"
#include "LogExtension.h"

TestClient& TestClient::GetInst()
{
	static TestClient instance;
	return instance;
}

bool TestClient::Start(const std::wstring& clientCoreOptionFile, const std::wstring& sessionGetterOptionFile)
{
	if (not RUDPClientCore::GetInst().Start(clientCoreOptionFile, sessionGetterOptionFile, true))
	{
		std::cout << "Core start failed" << '\n';
		return false;
	}

	if (constexpr unsigned int maximumConnectWaitingCount = 20; not WaitingConnectToServer(maximumConnectWaitingCount))
	{
		std::cout << "It was waiting " << maximumConnectWaitingCount << "seconds. But connect to server failed" << '\n';
		return false;
	}

	std::cout << "Client is running" << '\n';

	return true;
}

void TestClient::Stop()
{
	RUDPClientCore::GetInst().Stop();
}

bool TestClient::IsConnected()
{
	return RUDPClientCore::GetInst().IsConnected();
}

bool TestClient::WaitingConnectToServer(const unsigned int maximumConnectWaitingCount)
{
	unsigned int connectWaitingCount = 0;
	while (not RUDPClientCore::GetInst().IsConnected())
	{
		Sleep(1000);
		++connectWaitingCount;

		if (connectWaitingCount >= maximumConnectWaitingCount)
		{
			const auto log = Logger::MakeLogObject<ClientLog>();
			log->logString = "Connect to server failed";
			Logger::GetInstance().WriteLog(log);

			return false;
		}
	}

	return true;
}

void TestClient::SendPacket(char* streamData, const int streamSize)
{
	if (not RUDPClientCore::GetInst().IsConnected())
	{
		return;
	}

	RUDPClientCore::GetInst().SendPacketForTest(streamData, streamSize);
}

bool TestClient::GetStreamDataFromStoredPacket(char* outStreamData, int* outStreamSize, const int inStreamMaxSize)
{
	if (RUDPClientCore::GetInst().GetRemainPacketSize() == 0)
	{
		*outStreamSize = 0;
		return true;
	}

	NetBuffer* receivedPacket = RUDPClientCore::GetInst().GetReceivedPacket();
	if (receivedPacket == nullptr)
	{
		*outStreamSize = 0;
		return false;
	}

	const int useSize = receivedPacket->GetUseSize();
	if (inStreamMaxSize < useSize)
	{
		*outStreamSize = 0;
		return false;
	}

	*outStreamSize = useSize;
	receivedPacket->ReadBuffer(outStreamData, useSize);

	NetBuffer::Free(receivedPacket);

	return true;
}
