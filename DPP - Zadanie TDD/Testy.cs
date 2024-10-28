using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using Xunit;

namespace DPP___Zadanie_TDD
{

    // Obsługa nieistniejącej transakcji.
    public class Transaction(string userId, double amount, string transactionId, TransactionStatus status)
    {
        public string UserId { get; set; } = userId;
        public double Amount { get; set; } = amount;
        public string TransactionId { get; set; } = transactionId;
        public TransactionStatus Status { get; set; } = status;
    }

    public class Transactions
    {
        private readonly Dictionary<string, Transaction> _transactions = [];

        public Transaction this[string transactionId]
        {
            get
            {
                if (_transactions.TryGetValue(transactionId, out var transaction))
                {
                    return transaction;
                }
                throw new KeyNotFoundException($"Tranzakcja o ID {transactionId} nie istnieje.");
            }
            set
            {
                _transactions[transactionId] = value;
            }
        }

        public void AddTransaction(Transaction transaction)
        {
            _transactions[transaction.TransactionId] = transaction;
        }
    }


    public class PaymentGatewayMockStubSpy() : IPaymentGateway
    {
        // Weryfikacja wywołań: Weryfikacja liczby wywołań poszczególnych metod.
        public int ChargeCallCount { get; private set; } = 0;
        public int RefundCallCount { get; private set; } = 0;
        public int GetStatusCallCount { get; private set; } = 0;

        // Weryfikacja wywołań: Sprawdzenie, czy metody PaymentGateway zostały wywołane z oczekiwanymi parametrami.
        public List<(string userId, double amount)> ChargeParameters { get; private set; } = [];
        public List<string> RefundParameters { get; private set; } = [];
        public List<string> GetStatusParameters { get; private set; } = [];


        // Upewnienie się, że wyjątki z PaymentGateway nie powodują przerwania działania PaymentProcessor.
        // Sprawdzenie, czy wyjątki są obsługiwane i przekazywane w odpowiedni sposób.
        // Obsługa wyjątków - processPayment, refundPayment, getPaymentStatus.
        public bool ShouldThrowNetworkException { get; set; } = false;
        public bool ShouldThrowPaymentException { get; set; } = false;
        public bool ShouldThrowRefundException { get; set; } = false;

        // Obsługa nieistniejącej transakcji.
        public Transactions transactions = new();

        // processPayment: Niepowodzenie płatności z powodu braku środków.
        public bool IsEnoughMoney { get; set; } = true;


        public bool IsFakeResult { get; set; } = false;
        public TransactionResult FakeResult { get; set; } = new TransactionResult(false, "", "Simulated default result");

        public bool IsFakeStatus { get; set; } = false;
        public TransactionStatus FakeStatus { get; set; } = TransactionStatus.PENDING;

        public TransactionResult Charge(string userId, double amount)
        {
            ChargeCallCount++;
            ChargeParameters.Add((userId, amount));

            if (ShouldThrowNetworkException)
                throw new NetworkException("Błąd sieciowy po stronie providera");

            if (ShouldThrowPaymentException)
                throw new PaymentException("Błąd płatności");

            if (IsFakeResult)
                return FakeResult;

            var transactionId = Guid.NewGuid().ToString();
            transactions.AddTransaction(new Transaction(userId, amount, transactionId, TransactionStatus.PENDING));

            if (!IsEnoughMoney)
            {
                transactions[transactionId].Status = TransactionStatus.FAILED;
                return new TransactionResult(false, transactionId, "Brak środków na koncie");
            }

            transactions[transactionId].Status = TransactionStatus.COMPLETED;
            return new TransactionResult(true, transactionId, "Charge pomyślnie");
        }

        public TransactionResult Refund(string transactionId)
        {
            RefundCallCount++;
            RefundParameters.Add(transactionId);

            if (ShouldThrowNetworkException)
                throw new NetworkException("Błąd sieciowy po stronie providera");

            if (ShouldThrowRefundException)
                throw new RefundException("Błąd zwrotu");

            if (IsFakeResult)
                return FakeResult;

            try
            {
                var transaction = transactions[transactionId];
                transactions[transactionId].Status = TransactionStatus.PENDING;
            }
            catch (KeyNotFoundException ex)
            {
                return new TransactionResult(false, "", ex.Message);
            }

            transactions[transactionId].Status = TransactionStatus.COMPLETED;
            return new TransactionResult(true, transactionId, "Refund pomyślnie");
        }

        public TransactionStatus GetStatus(string transactionId)
        {
            GetStatusCallCount++;
            GetStatusParameters.Add(transactionId);

            if (ShouldThrowNetworkException)
                throw new NetworkException("Błąd sieciowy po stronie providera");

            if (IsFakeStatus)
                return FakeStatus;

            try
            {
                var transaction = transactions[transactionId];
                return transaction.Status;
            }
            catch (KeyNotFoundException)
            {
                return TransactionStatus.FAILED;
            }

           
        }
    }


    public class  PaymentProcessorBasicTests
    {
        public required PaymentProcessor _paymentProcessor;
        public required PaymentGatewayMockStubSpy _paymentGatewaySpy;
        public required string _userId = "gacek";
        public required double _amount = 100;

        public PaymentProcessorBasicTests()
        {
            _paymentGatewaySpy = new PaymentGatewayMockStubSpy();
            _paymentProcessor = new PaymentProcessor(_paymentGatewaySpy);
        }



        // processPayment | Prawidłowe przetworzenie płatności
        [Fact]
        public void ProcessPayment_Success()
        {
            var result = _paymentProcessor.ProcessPayment(_userId, _amount);

            Assert.True(result.Success);
            Assert.Equal("Charge pomyślnie", result.Message);
            Assert.NotEmpty(result.TransactionId);
        }

        // processPayment | Niepowodzenie płatności z powodu braku środków
        [Fact]
        public void ProcessPayment_NotEnoughMoney()
        {
            _paymentGatewaySpy.IsEnoughMoney = false;
            var result = _paymentProcessor.ProcessPayment(_userId, _amount);

            Assert.False(result.Success);
            Assert.Equal("Brak środków na koncie", result.Message);
            Assert.NotEmpty(result.TransactionId);
        }

        // processPayment | Obsługa wyjątków NetworkException i PaymentException | NetworkException
        // Symulowanie rzucania wyjątków przez metody PaymentGateway.
        [Fact]
        public void ProcessPayment_NetworkException()
        {
            _paymentGatewaySpy.ShouldThrowNetworkException = true;
            var result = _paymentProcessor.ProcessPayment(_userId, _amount);

            Assert.False(result.Success);
            Assert.Equal("NetworkException: Błąd sieciowy po stronie providera", result.Message);
            Assert.Empty(result.TransactionId);
        }

        // processPayment | Obsługa wyjątków NetworkException i PaymentException | PaymentException
        // Symulowanie rzucania wyjątków przez metody PaymentGateway.
        [Fact]
        public void ProcessPayment_PaymentException()
        {
            _paymentGatewaySpy.ShouldThrowPaymentException = true;
            var result = _paymentProcessor.ProcessPayment(_userId, _amount);

            Assert.False(result.Success);
            Assert.Equal("PaymentException: Błąd płatności", result.Message);
            Assert.Empty(result.TransactionId);
        }


        // processPayment | Walidacja nieprawidłowych danych wejściowych | Pusty userId
        [Fact]
        public void ProcessPayment_EmptyUserId()
        {
            var result = _paymentProcessor.ProcessPayment("", _amount);

            Assert.False(result.Success);
            Assert.Equal("Pusty user Id", result.Message);
            Assert.Empty(result.TransactionId);
        }

        // processPayment | Walidacja nieprawidłowych danych wejściowych | Zerowy amount
        [Fact]
        public void ProcessPayment_EmptyAmount()
        {
            var result = _paymentProcessor.ProcessPayment(_userId, 0);

            Assert.False(result.Success);
            Assert.Equal("Amount <= 0", result.Message);
            Assert.Empty(result.TransactionId);
        }

        // processPayment | Walidacja nieprawidłowych danych wejściowych | Ujemny amount
        [Fact]
        public void ProcessPayment_NegativeAmount()
        {
            var result = _paymentProcessor.ProcessPayment(_userId, -_amount);

            Assert.False(result.Success);
            Assert.Equal("Amount <= 0", result.Message);
            Assert.Empty(result.TransactionId);
        }

        // refundPayment | Prawidłowe dokonanie zwrotu
        [Fact]
        public void RefundPayment_Success()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            var result = _paymentProcessor.RefundPayment(chargeResult.TransactionId);

            Assert.True(result.Success);
            Assert.Equal("Refund pomyślnie", result.Message);
            Assert.NotEmpty(result.TransactionId);
        }

        // refundPayment | Niepowodzenie zwrotu z powodu nieistniejącej transakcji
        [Fact]
        public void RefundPayment_NonExistingTransaction()
        {
            var result = _paymentProcessor.RefundPayment("nonExistingTransactionId");

            Assert.False(result.Success);
            Assert.Equal("Tranzakcja o ID nonExistingTransactionId nie istnieje.", result.Message);
            Assert.Empty(result.TransactionId);
        }

        // refundPayment | Obsługa wyjątków NetworkException i RefundException | NetworkException
        // Symulowanie rzucania wyjątków przez metody PaymentGateway.
        [Fact]
        public void RefundPayment_NetworkException()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            _paymentGatewaySpy.ShouldThrowNetworkException = true;
            var result = _paymentProcessor.RefundPayment(chargeResult.TransactionId);

            Assert.False(result.Success);
            Assert.Equal("NetworkException: Błąd sieciowy po stronie providera", result.Message);
            Assert.Empty(result.TransactionId);
        }

        // getPaymentStatus || Pobranie poprawnego statusu transakcji.
        [Fact]
        public void GetPaymentStatus_Success()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            var result = _paymentProcessor.GetPaymentStatus(chargeResult.TransactionId);

            Assert.Equal(TransactionStatus.COMPLETED, result);
        }

        // getPaymentStatus || Obsługa nieistniejącej transakcji.
        [Fact]
        public void GetPaymentStatus_NonExistingTransaction()
        {
            var result = _paymentProcessor.GetPaymentStatus("nonExistingTransactionId");

            Assert.Equal(TransactionStatus.FAILED, result);
        }

        // getPaymentStatus || Obsługa wyjątków NetworkException.
        [Fact]
        public void GetPaymentStatus_NetworkException()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            _paymentGatewaySpy.ShouldThrowNetworkException = true;
            var result = _paymentProcessor.GetPaymentStatus(chargeResult.TransactionId);

            Assert.Equal(TransactionStatus.FAILED, result);
        }

        // Symulowanie różnych odpowiedzi metod charge, refund i getStatus.
        // Konfiguracja zwracanych wartości TransactionResult i TransactionStatus.
        [Fact]
        public void ProcessPayment_SimulatedResult()
        {
            _paymentGatewaySpy.IsFakeResult = true;
            _paymentGatewaySpy.FakeResult = new TransactionResult(true, "T-1", "Prawidłowa tranzakcja");

            var result = _paymentProcessor.ProcessPayment(_userId, _amount);
            Assert.True(result.Success);
            Assert.Equal("Prawidłowa tranzakcja", result.Message);
            Assert.Equal("T-1", result.TransactionId);

            result = _paymentProcessor.RefundPayment(result.TransactionId);
            Assert.True(result.Success);
            Assert.Equal("Prawidłowa tranzakcja", result.Message);
            Assert.Equal("T-1", result.TransactionId);

            _paymentGatewaySpy.FakeResult = new TransactionResult(false, "T-1", "Błędna tranzakcja");

            result = _paymentProcessor.ProcessPayment(_userId, _amount);
            Assert.False(result.Success);
            Assert.Equal("Błędna tranzakcja", result.Message);
            Assert.Equal("T-1", result.TransactionId);

            result = _paymentProcessor.RefundPayment(result.TransactionId);
            Assert.False(result.Success);
            Assert.Equal("Błędna tranzakcja", result.Message);
            Assert.Equal("T-1", result.TransactionId);


            _paymentGatewaySpy.IsFakeStatus = true;
            _paymentGatewaySpy.FakeStatus = TransactionStatus.COMPLETED;

            var resultStatus = _paymentProcessor.GetPaymentStatus("T-1");
            Assert.Equal(TransactionStatus.COMPLETED, resultStatus);

            _paymentGatewaySpy.FakeStatus = TransactionStatus.FAILED;
            Assert.Equal(TransactionStatus.FAILED, _paymentProcessor.GetPaymentStatus("T-1"));

            _paymentGatewaySpy.FakeStatus = TransactionStatus.PENDING;
            Assert.Equal(TransactionStatus.PENDING, _paymentProcessor.GetPaymentStatus("T-1"));
        }


        // Sprawdzenie, czy metody PaymentGateway zostały wywołane z oczekiwanymi parametrami. | ProcessPayment
        [Fact]
        public void ProcessPayment_Parameters()
        {
            _paymentProcessor.ProcessPayment(_userId, _amount);
            Assert.Equal((_userId, _amount), _paymentGatewaySpy.ChargeParameters[0]);
        }

        // Sprawdzenie, czy metody PaymentGateway zostały wywołane z oczekiwanymi parametrami. | RefundPayment
        [Fact]
        public void RefundPayment_Parameters()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            _paymentProcessor.RefundPayment(chargeResult.TransactionId);
            Assert.Equal(chargeResult.TransactionId, _paymentGatewaySpy.RefundParameters[0]);
        }

        // Sprawdzenie, czy metody PaymentGateway zostały wywołane z oczekiwanymi parametrami. | GetPaymentStatus
        [Fact]
        public void GetPaymentStatus_Parameters()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            _paymentProcessor.GetPaymentStatus(chargeResult.TransactionId);
            Assert.Equal(chargeResult.TransactionId, _paymentGatewaySpy.GetStatusParameters[0]);
        }

        // Weryfikacja liczby wywołań poszczególnych metod. | ProcessPayment
        [Fact]
        public void ProcessPayment_CallCount()
        {
            Assert.Equal(0, _paymentGatewaySpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewaySpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewaySpy.GetStatusCallCount);

            _paymentProcessor.ProcessPayment(_userId, _amount);
            Assert.Equal(1, _paymentGatewaySpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewaySpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewaySpy.GetStatusCallCount);

            _paymentProcessor.ProcessPayment(_userId, _amount);
            Assert.Equal(2, _paymentGatewaySpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewaySpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewaySpy.GetStatusCallCount);

            for (int i = 0; i < 10; i++)
            {
                _paymentProcessor.ProcessPayment(_userId, _amount);
            }

            Assert.Equal(12, _paymentGatewaySpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewaySpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewaySpy.GetStatusCallCount);
        }

        // Weryfikacja liczby wywołań poszczególnych metod. | RefundPayment
        [Fact]
        public void ProcessPayment_NotCalled() {
            Assert.Equal(0, _paymentGatewaySpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewaySpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewaySpy.GetStatusCallCount);

            _paymentProcessor.RefundPayment("T-1");
            Assert.Equal(0, _paymentGatewaySpy.ChargeCallCount);
            Assert.Equal(1, _paymentGatewaySpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewaySpy.GetStatusCallCount);


            _paymentProcessor.RefundPayment("T-1");
            Assert.Equal(0, _paymentGatewaySpy.ChargeCallCount);
            Assert.Equal(2, _paymentGatewaySpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewaySpy.GetStatusCallCount);

            for (int i = 0; i < 10; i++)
            {
                _paymentProcessor.RefundPayment("T-1");
            }

            Assert.Equal(0, _paymentGatewaySpy.ChargeCallCount);
            Assert.Equal(12, _paymentGatewaySpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewaySpy.GetStatusCallCount);
        }

        // Weryfikacja liczby wywołań poszczególnych metod. | GetPaymentStatus
        [Fact]
        public void GetPaymentStatus_CallCount()
        {
            Assert.Equal(0, _paymentGatewaySpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewaySpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewaySpy.GetStatusCallCount);

            _paymentProcessor.GetPaymentStatus("T-1");
            Assert.Equal(0, _paymentGatewaySpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewaySpy.RefundCallCount);
            Assert.Equal(1, _paymentGatewaySpy.GetStatusCallCount);

            _paymentProcessor.GetPaymentStatus("T-1");
            Assert.Equal(0, _paymentGatewaySpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewaySpy.RefundCallCount);
            Assert.Equal(2, _paymentGatewaySpy.GetStatusCallCount);

            for (int i = 0; i < 10; i++)
            {
                _paymentProcessor.GetPaymentStatus("T-1");
            }

            Assert.Equal(0, _paymentGatewaySpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewaySpy.RefundCallCount);
            Assert.Equal(12, _paymentGatewaySpy.GetStatusCallCount);
        }

    }

}
