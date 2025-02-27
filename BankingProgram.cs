using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

class BankAccount
{
    private readonly object lockObj = new object();
    private int balance;

    public string AccountName { get; }

    public BankAccount(string name, int initialBalance)
    {
        AccountName = name;
        balance = initialBalance;
    }

    public void Deposit(int amount)
    {
        lock (lockObj)
        {
            balance += amount;
            Console.WriteLine($"{AccountName}: Deposited {amount}, Balance: {balance}");
        }
    }

    public void Withdraw(int amount)
    {
        lock (lockObj)
        {
            if (balance >= amount)
            {
                balance -= amount;
                Console.WriteLine($"{AccountName}: Withdrawn {amount}, Balance: {balance}");
            }
            else
            {
                Console.WriteLine($"{AccountName}: Insufficient funds for {amount}");
            }
        }
    }

    public static void Transfer(BankAccount from, BankAccount to, int amount)
    {
        lock (from.lockObj)
        {
            Thread.Sleep(100);
            lock (to.lockObj)
            {
                if (from.balance >= amount)
                {
                    from.balance -= amount;
                    to.balance += amount;
                    Console.WriteLine($"Transferred {amount} from {from.AccountName} to {to.AccountName}");
                }
                else
                {
                    Console.WriteLine($"Transfer failed: Insufficient funds in {from.AccountName}");
                }
            }
        }
    }

    public static void SafeTransfer(BankAccount from, BankAccount to, int amount)
    {
        object firstLock = from.lockObj.GetHashCode() < to.lockObj.GetHashCode() ? from.lockObj : to.lockObj;
        object secondLock = from.lockObj.GetHashCode() < to.lockObj.GetHashCode() ? to.lockObj : from.lockObj;
        lock (firstLock)
        {
            Thread.Sleep(100);
            lock (secondLock)
            {
                if (from.balance >= amount)
                {
                    from.balance -= amount;
                    to.balance += amount;
                    Console.WriteLine($"Safe Transfer: {amount} from {from.AccountName} to {to.AccountName}");
                }
                else
                {
                    Console.WriteLine($"Safe Transfer failed: Insufficient funds in {from.AccountName}");
                }
            }
        }
    }
}

class Program
{
    static void Main()
    {
        string pipeName = "testpipe";
        BankAccount acc1 = new BankAccount("Account1", 1000);
        BankAccount acc2 = new BankAccount("Account2", 1000);

        Thread producerThread = new Thread(() => RunProducer(pipeName));
        Thread consumerThread = new Thread(() => RunConsumer(pipeName, acc1, acc2));

        producerThread.Start();
        Thread.Sleep(500);
        consumerThread.Start();

        producerThread.Join();
        consumerThread.Join();
    }

    static void RunProducer(string pipeName)
    {
        try
        {
            using (NamedPipeServerStream pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.Out))
            {
                Console.WriteLine("[Producer] Waiting for connection...");
                pipeServer.WaitForConnection();
                Console.WriteLine("[Producer] Connected to consumer.");

                Thread[] threads = new Thread[5];
                for (int i = 0; i < threads.Length; i++)
                {
                    int messageNumber = i + 1;
                    threads[i] = new Thread(() => SendMessage(pipeServer, messageNumber));
                    threads[i].Start();
                }

                foreach (var thread in threads)
                {
                    thread.Join();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Producer] Error: " + ex.Message);
        }
    }

    static void SendMessage(NamedPipeServerStream pipeServer, int messageNumber)
    {
        try
        {
            lock (pipeServer)
            {
                using (StreamWriter writer = new StreamWriter(pipeServer))
                {
                    writer.AutoFlush = true;
                    string message = $"Message {messageNumber}";
                    Console.WriteLine("[Producer Thread] Sending: " + message);
                    writer.WriteLine(message);
                    Thread.Sleep(1000);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Producer Thread] Error: " + ex.Message);
        }
    }
static void RunConsumer(string pipeName, BankAccount acc1, BankAccount acc2)
{
    try
    {
        using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.In))
        {
            if (pipeClient != null)
            {
                Console.WriteLine("[Consumer] Connecting to producer...");
                pipeClient.Connect();
                Console.WriteLine("[Consumer] Connected to producer.");

                byte[] buffer = new byte[256];
                int bytesRead;

                while ((bytesRead = pipeClient.Read(buffer, 0, buffer.Length)) > 0)
                {
                    string message = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine("[Consumer] Received: " + message);

                    // Perform bank account operations
                    acc1.Deposit(100);
                    acc2.Withdraw(50);
                    BankAccount.Transfer(acc1, acc2, 200);
                    BankAccount.SafeTransfer(acc2, acc1, 100);
                }
            }
            else
            {
                Console.WriteLine("[Consumer] Error: pipeClient is null");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("[Consumer] Error: " + ex.Message);
    }
}
}
