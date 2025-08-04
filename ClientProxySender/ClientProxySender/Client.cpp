#include "PreCompile.h"
#include "Client.h"
#include "RUDPClientCore.h"
#include "Logger.h"
#include "LogExtension.h"
#include "../ContentsClient/Protocol.h"

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

	testThread = std::thread{ &TestClient::RunTestThread, this };
	return true;
}

void TestClient::Stop()
{
	if (testThread.joinable())
	{
		testThread.join();
	}
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

void TestClient::RunTestThread()
{
	if (not WaitingConnectToServer(10))
	{
		return;
	}

	while (RUDPClientCore::GetInst().IsConnected())
	{
		Sleep(1000);
	}
}