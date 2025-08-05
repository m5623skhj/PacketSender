#pragma once
#include <thread>
#include "NetServerSerializeBuffer.h"
#include <mutex>
#include <set>

class TestClient
{
private:
	TestClient() = default;
	~TestClient() = default;
	TestClient& operator=(const TestClient&) = delete;
	TestClient(TestClient&&) = delete;

public:
	static TestClient& GetInst();

public:
	bool Start(const std::wstring& clientCoreOptionFile, const std::wstring& sessionGetterOptionFile);
	void Stop();
	static bool IsConnected();

public:
	static void SendPacket(char* streamData, int streamSize);

private:
	static bool WaitingConnectToServer(unsigned int maximumConnectWaitingCount);
	static void RunTestThread();

private:
	std::jthread testThread;

private:
	std::atomic_int order{ 0 };
	std::list<int> orderList{};
	std::mutex orderListLock;
	std::multiset<std::string> echoStringSet{};
	std::mutex echoStringSetLock;
};