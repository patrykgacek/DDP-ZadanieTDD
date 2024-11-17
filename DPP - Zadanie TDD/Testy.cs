using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using Xunit;

namespace DPP___Zadanie_TDD
{

    public class PaymentGatewayMockStubSpy() : IPaymentGateway
    {
        // Verification of calls: Verification of the number of calls to individual methods.
        public int ChargeCallCount { get; private set; } = 0;
        public int RefundCallCount { get; private set; } = 0;
        public int GetStatusCallCount { get; private set; } = 0;

        // Verification of calls: Checking if the PaymentGateway methods were called with the expected parameters.
        public List<(string userId, double amount)> ChargeParameters { get; private set; } = [];
        public List<string> RefundParameters { get; private set; } = [];
        public List<string> GetStatusParameters { get; private set; } = [];


        // Ensuring that exceptions from PaymentGateway do not interrupt the operation of PaymentProcessor.
        // Checking if exceptions are handled and passed appropriately.
        // Exception handling - processPayment, refundPayment, getPaymentStatus.
        public bool ShouldThrowNetworkException { get; set; } = false;
        public bool ShouldThrowPaymentException { get; set; } = false;
        public bool ShouldThrowRefundException { get; set; } = false;


        // processPayment: Payment failure due to insufficient funds.
        public bool IsEnoughMoney { get; set; } = true;

        // Handling of a non-existent transaction.
        public string TransactionIdStub { get; set; } = Guid.NewGuid().ToString();


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

            if (!IsEnoughMoney)
            {
                return new TransactionResult(false, transactionId, "Brak środków na koncie");
            }

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

            if (this.TransactionIdStub != transactionId)
                return new TransactionResult(false, "", "Tranzakcja o ID " + transactionId + " nie istnieje.");

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

            if (this.TransactionIdStub != transactionId)
                return TransactionStatus.FAILED;

            return TransactionStatus.COMPLETED;
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



        // processPayment | Correct processing of the payment.
        [Fact]
        public void ProcessPayment_Success()
        {
            var result = _paymentProcessor.ProcessPayment(_userId, _amount);

            Assert.True(result.Success);
            Assert.Equal("Charge pomyślnie", result.Message);
            Assert.NotEmpty(result.TransactionId);
        }

        // processPayment | Payment failure due to insufficient funds.
        [Fact]
        public void ProcessPayment_NotEnoughMoney()
        {
            _paymentGatewaySpy.IsEnoughMoney = false;
            var result = _paymentProcessor.ProcessPayment(_userId, _amount);

            Assert.False(result.Success);
            Assert.Equal("Brak środków na koncie", result.Message);
            Assert.NotEmpty(result.TransactionId);
        }

        // processPayment | Handling of NetworkException and PaymentException | NetworkException
        // Simulating throwing exceptions by PaymentGateway methods.
        [Fact]
        public void ProcessPayment_NetworkException()
        {
            _paymentGatewaySpy.ShouldThrowNetworkException = true;
            var result = _paymentProcessor.ProcessPayment(_userId, _amount);

            Assert.False(result.Success);
            Assert.Equal("NetworkException: Błąd sieciowy po stronie providera", result.Message);
            Assert.Empty(result.TransactionId);
        }

        // processPayment | Handling of NetworkException and PaymentException | PaymentException
        // Simulating throwing exceptions by PaymentGateway methods.
        [Fact]
        public void ProcessPayment_PaymentException()
        {
            _paymentGatewaySpy.ShouldThrowPaymentException = true;
            var result = _paymentProcessor.ProcessPayment(_userId, _amount);

            Assert.False(result.Success);
            Assert.Equal("PaymentException: Błąd płatności", result.Message);
            Assert.Empty(result.TransactionId);
        }


        // processPayment | Validation of invalid input data | Empty userId
        [Fact]
        public void ProcessPayment_EmptyUserId()
        {
            var result = _paymentProcessor.ProcessPayment("", _amount);

            Assert.False(result.Success);
            Assert.Equal("Pusty user Id", result.Message);
            Assert.Empty(result.TransactionId);
        }

        // processPayment | Validation of invalid input data | Zero amount
        [Fact]
        public void ProcessPayment_EmptyAmount()
        {
            var result = _paymentProcessor.ProcessPayment(_userId, 0);

            Assert.False(result.Success);
            Assert.Equal("Amount <= 0", result.Message);
            Assert.Empty(result.TransactionId);
        }

        // processPayment | Validation of invalid input data | Negative amount
        [Fact]
        public void ProcessPayment_NegativeAmount()
        {
            var result = _paymentProcessor.ProcessPayment(_userId, -_amount);

            Assert.False(result.Success);
            Assert.Equal("Amount <= 0", result.Message);
            Assert.Empty(result.TransactionId);
        }

        // refundPayment | Correct processing of the refund
        [Fact]
        public void RefundPayment_Success()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            var result = _paymentProcessor.RefundPayment(chargeResult.TransactionId);

            Assert.True(result.Success);
            Assert.Equal("Refund pomyślnie", result.Message);
            Assert.NotEmpty(result.TransactionId);
        }

        // refundPayment | Refund failure due to non-existent transaction
        [Fact]
        public void RefundPayment_NonExistingTransaction()
        {
            var result = _paymentProcessor.RefundPayment("nonExistingTransactionId");

            Assert.False(result.Success);
            Assert.Equal("Tranzakcja o ID nonExistingTransactionId nie istnieje.", result.Message);
            Assert.Empty(result.TransactionId);
        }

        // refundPayment | Handling of NetworkException and RefundException | NetworkException
        // Simulating throwing exceptions by PaymentGateway methods.
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

        // getPaymentStatus || Retrieving the correct transaction status.
        [Fact]
        public void GetPaymentStatus_Success()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            var result = _paymentProcessor.GetPaymentStatus(chargeResult.TransactionId);

            Assert.Equal(TransactionStatus.COMPLETED, result);
        }

        // getPaymentStatus || Handling of a non-existent transaction.
        [Fact]
        public void GetPaymentStatus_NonExistingTransaction()
        {
            var result = _paymentProcessor.GetPaymentStatus("nonExistingTransactionId");

            Assert.Equal(TransactionStatus.FAILED, result);
        }

        // getPaymentStatus || Handling of NetworkException.
        [Fact]
        public void GetPaymentStatus_NetworkException()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            _paymentGatewaySpy.ShouldThrowNetworkException = true;
            var result = _paymentProcessor.GetPaymentStatus(chargeResult.TransactionId);

            Assert.Equal(TransactionStatus.FAILED, result);
        }

        // Simulating different responses from the charge, refund, and getStatus methods.
        // Configuration of returned values for TransactionResult and TransactionStatus.
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


        // Checking if the PaymentGateway methods were called with the expected parameters. | ProcessPayment
        [Fact]
        public void ProcessPayment_Parameters()
        {
            _paymentProcessor.ProcessPayment(_userId, _amount);
            Assert.Equal((_userId, _amount), _paymentGatewaySpy.ChargeParameters[0]);
        }

        // Checking if the PaymentGateway methods were called with the expected parameters. | RefundPayment
        [Fact]
        public void RefundPayment_Parameters()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            _paymentProcessor.RefundPayment(chargeResult.TransactionId);
            Assert.Equal(chargeResult.TransactionId, _paymentGatewaySpy.RefundParameters[0]);
        }

        // Checking if the PaymentGateway methods were called with the expected parameters. | GetPaymentStatus
        [Fact]
        public void GetPaymentStatus_Parameters()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            _paymentProcessor.GetPaymentStatus(chargeResult.TransactionId);
            Assert.Equal(chargeResult.TransactionId, _paymentGatewaySpy.GetStatusParameters[0]);
        }

        // Verification of the number of calls to individual methods. | ProcessPayment
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

        // Verification of the number of calls to individual methods. | RefundPayment
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

        // Verification of the number of calls to individual methods. | GetPaymentStatus
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
