using DPP___Zadanie_TDD;
using Xunit;


namespace TestProject
{
    public class LoggerMock : ILogger
    {
        public List<string> Messages { get; private set; } = [];

        public void Log(string message)
        {
            Messages.Add(message);
        }
    }

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
                throw new NetworkException("Provider network problem");

            if (ShouldThrowPaymentException)
                throw new PaymentException("Provider payment problem");

            if (IsFakeResult)
                return FakeResult;

            var transactionId = Guid.NewGuid().ToString();

            if (!IsEnoughMoney)
            {
                return new TransactionResult(false, transactionId, "Not enough money in the account");
            }

            return new TransactionResult(true, transactionId, "Charge success");
        }

        public TransactionResult Refund(string transactionId)
        {
            RefundCallCount++;
            RefundParameters.Add(transactionId);

            if (ShouldThrowNetworkException)
                throw new NetworkException("Provider network problem");

            if (ShouldThrowRefundException)
                throw new RefundException("Provider refund problem");

            if (IsFakeResult)
                return FakeResult;

            if (this.TransactionIdStub != transactionId)
                return new TransactionResult(false, "", "");

            return new TransactionResult(true, transactionId, "Refund success");
        }

        public TransactionStatus GetStatus(string transactionId)
        {
            GetStatusCallCount++;
            GetStatusParameters.Add(transactionId);

            if (ShouldThrowNetworkException)
                throw new NetworkException("Provider network problem");

            if (IsFakeStatus)
                return FakeStatus;

            if (this.TransactionIdStub != transactionId)
                return TransactionStatus.FAILED;

            return TransactionStatus.COMPLETED;
        }
    }


    public class PaymentProcessorBasicTests
    {
        public required LoggerMock _loggerMock;
        public required PaymentProcessor _paymentProcessor;
        public required PaymentGatewayMockStubSpy _paymentGatewayMockStubSpy;
        public required string _userId = "gacek";
        public required double _amount = 100;

        public PaymentProcessorBasicTests()
        {
            _loggerMock = new LoggerMock();
            _paymentGatewayMockStubSpy = new PaymentGatewayMockStubSpy();
            _paymentProcessor = new PaymentProcessor(_paymentGatewayMockStubSpy, _loggerMock);

        }



        // processPayment | Correct processing of the payment.
        [Fact]
        public void ProcessPayment_Success()
        {
            var result = _paymentProcessor.ProcessPayment(_userId, _amount);

            Assert.True(result.Success);
            Assert.NotEmpty(result.TransactionId);
            Assert.Equal("ProcessPayment successful", _loggerMock.Messages[0]);
        }

        // processPayment | Payment failure due to insufficient funds.
        [Fact]
        public void ProcessPayment_NotEnoughMoney()
        {
            _paymentGatewayMockStubSpy.IsEnoughMoney = false;
            var result = _paymentProcessor.ProcessPayment(_userId, _amount);

            Assert.False(result.Success);
            Assert.NotEmpty(result.TransactionId);
            Assert.Equal("ProcessPayment failed", _loggerMock.Messages[0]);
        }

        // processPayment | Handling of NetworkException and PaymentException | NetworkException
        // Simulating throwing exceptions by PaymentGateway methods.
        [Fact]
        public void ProcessPayment_NetworkException()
        {
            _paymentGatewayMockStubSpy.ShouldThrowNetworkException = true;
            var result = _paymentProcessor.ProcessPayment(_userId, _amount);

            Assert.False(result.Success);
            Assert.Empty(result.TransactionId);
            Assert.Equal("ProcessPayment NetworkException", _loggerMock.Messages[0]);
        }

        // processPayment | Handling of NetworkException and PaymentException | PaymentException
        // Simulating throwing exceptions by PaymentGateway methods.
        [Fact]
        public void ProcessPayment_PaymentException()
        {
            _paymentGatewayMockStubSpy.ShouldThrowPaymentException = true;
            var result = _paymentProcessor.ProcessPayment(_userId, _amount);

            Assert.False(result.Success);
            Assert.Empty(result.TransactionId);
            Assert.Equal("ProcessPayment PaymentException", _loggerMock.Messages[0]);
        }


        // processPayment | Validation of invalid input data | Empty userId
        [Fact]
        public void ProcessPayment_EmptyUserId()
        {
            var result = _paymentProcessor.ProcessPayment("", _amount);

            Assert.False(result.Success);
            Assert.Empty(result.TransactionId);
            Assert.Equal("userId not provided", _loggerMock.Messages[0]);
        }

        // processPayment | Validation of invalid input data | Zero amount
        [Fact]
        public void ProcessPayment_EmptyAmount()
        {
            var result = _paymentProcessor.ProcessPayment(_userId, 0);

            Assert.False(result.Success);
            Assert.Empty(result.TransactionId);
            Assert.Equal("Amount negative or zero", _loggerMock.Messages[0]);
        }

        // processPayment | Validation of invalid input data | Negative amount
        [Fact]
        public void ProcessPayment_NegativeAmount()
        {
            var result = _paymentProcessor.ProcessPayment(_userId, -_amount);

            Assert.False(result.Success);
            Assert.Empty(result.TransactionId);
            Assert.Equal("Amount negative or zero", _loggerMock.Messages[0]);
        }

        // refundPayment | Correct processing of the refund
        [Fact]
        public void RefundPayment_Success()
        {
            var transactionId = _paymentGatewayMockStubSpy.TransactionIdStub;
            var result = _paymentProcessor.RefundPayment(transactionId);

            Assert.True(result.Success);
            Assert.NotEmpty(result.TransactionId);
            Assert.Equal("RefundPayment successful", _loggerMock.Messages[0]);
        }

        // refundPayment | Refund failure due to non-existent transaction
        [Fact]
        public void RefundPayment_NonExistingTransaction()
        {
            var result = _paymentProcessor.RefundPayment("nonExistingTransactionId");

            Assert.False(result.Success);
            Assert.Empty(result.TransactionId);
            Assert.Equal("RefundPayment failed", _loggerMock.Messages[0]);
        }

        // refundPayment | Handling of NetworkException and RefundException | NetworkException
        // Simulating throwing exceptions by PaymentGateway methods.
        [Fact]
        public void RefundPayment_NetworkException()
        {
            var transactionId = _paymentGatewayMockStubSpy.TransactionIdStub;
            _paymentGatewayMockStubSpy.ShouldThrowNetworkException = true;
            var result = _paymentProcessor.RefundPayment(transactionId);

            Assert.False(result.Success);
            Assert.Empty(result.TransactionId);
            Assert.Equal("RefundPayment NetworkException", _loggerMock.Messages[0]);
        }

        // getPaymentStatus || Retrieving the correct transaction status.
        [Fact]
        public void GetPaymentStatus_Success()
        {
            var transactionId = _paymentGatewayMockStubSpy.TransactionIdStub;
            var result = _paymentProcessor.GetPaymentStatus(transactionId);

            Assert.Equal(TransactionStatus.COMPLETED, result);
            Assert.Equal("GetPaymentStatus successful", _loggerMock.Messages[0]);
        }

        // getPaymentStatus || Handling of a non-existent transaction.
        [Fact]
        public void GetPaymentStatus_NonExistingTransaction()
        {
            var result = _paymentProcessor.GetPaymentStatus("nonExistingTransactionId");
            Assert.Equal(TransactionStatus.FAILED, result);
            Assert.Equal("GetPaymentStatus successful", _loggerMock.Messages[0]);
        }

        // getPaymentStatus || Handling of NetworkException.
        [Fact]
        public void GetPaymentStatus_NetworkException()
        {
            var transactionId = _paymentGatewayMockStubSpy.TransactionIdStub;
            _paymentGatewayMockStubSpy.ShouldThrowNetworkException = true;
            var result = _paymentProcessor.GetPaymentStatus(transactionId);

            Assert.Equal(TransactionStatus.FAILED, result);
            Assert.Equal("GetPaymentStatus NetworkException", _loggerMock.Messages[0]);
        }

        // Simulating different responses from the charge, refund, and getStatus methods.
        // Configuration of returned values for TransactionResult and TransactionStatus.
        [Fact]
        public void ProcessPayment_SimulatedResult()
        {
            _paymentGatewayMockStubSpy.IsFakeResult = true;
            _paymentGatewayMockStubSpy.FakeResult = new TransactionResult(true, "T-1", "");

            var result = _paymentProcessor.ProcessPayment(_userId, _amount);
            Assert.True(result.Success);
            Assert.Equal("T-1", result.TransactionId);

            result = _paymentProcessor.RefundPayment(result.TransactionId);
            Assert.True(result.Success);
            Assert.Equal("T-1", result.TransactionId);

            _paymentGatewayMockStubSpy.FakeResult = new TransactionResult(false, "T-1", "");

            result = _paymentProcessor.ProcessPayment(_userId, _amount);
            Assert.False(result.Success);
            Assert.Equal("T-1", result.TransactionId);

            result = _paymentProcessor.RefundPayment(result.TransactionId);
            Assert.False(result.Success);
            Assert.Equal("T-1", result.TransactionId);


            _paymentGatewayMockStubSpy.IsFakeStatus = true;
            _paymentGatewayMockStubSpy.FakeStatus = TransactionStatus.COMPLETED;

            var resultStatus = _paymentProcessor.GetPaymentStatus("T-1");
            Assert.Equal(TransactionStatus.COMPLETED, resultStatus);

            _paymentGatewayMockStubSpy.FakeStatus = TransactionStatus.FAILED;
            Assert.Equal(TransactionStatus.FAILED, _paymentProcessor.GetPaymentStatus("T-1"));

            _paymentGatewayMockStubSpy.FakeStatus = TransactionStatus.PENDING;
            Assert.Equal(TransactionStatus.PENDING, _paymentProcessor.GetPaymentStatus("T-1"));
        }


        // Checking if the PaymentGateway methods were called with the expected parameters. | ProcessPayment
        [Fact]
        public void ProcessPayment_Parameters()
        {
            _paymentProcessor.ProcessPayment(_userId, _amount);
            Assert.Equal((_userId, _amount), _paymentGatewayMockStubSpy.ChargeParameters[0]);
        }

        // Checking if the PaymentGateway methods were called with the expected parameters. | RefundPayment
        [Fact]
        public void RefundPayment_Parameters()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            _paymentProcessor.RefundPayment(chargeResult.TransactionId);
            Assert.Equal(chargeResult.TransactionId, _paymentGatewayMockStubSpy.RefundParameters[0]);
        }

        // Checking if the PaymentGateway methods were called with the expected parameters. | GetPaymentStatus
        [Fact]
        public void GetPaymentStatus_Parameters()
        {
            var chargeResult = _paymentProcessor.ProcessPayment(_userId, _amount);
            _paymentProcessor.GetPaymentStatus(chargeResult.TransactionId);
            Assert.Equal(chargeResult.TransactionId, _paymentGatewayMockStubSpy.GetStatusParameters[0]);
        }

        // Verification of the number of calls to individual methods. | ProcessPayment
        [Fact]
        public void ProcessPayment_CallCount()
        {
            Assert.Equal(0, _paymentGatewayMockStubSpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.GetStatusCallCount);

            _paymentProcessor.ProcessPayment(_userId, _amount);
            Assert.Equal(1, _paymentGatewayMockStubSpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.GetStatusCallCount);

            _paymentProcessor.ProcessPayment(_userId, _amount);
            Assert.Equal(2, _paymentGatewayMockStubSpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.GetStatusCallCount);

            for (int i = 0; i < 10; i++)
            {
                _paymentProcessor.ProcessPayment(_userId, _amount);
            }

            Assert.Equal(12, _paymentGatewayMockStubSpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.GetStatusCallCount);
        }

        // Verification of the number of calls to individual methods. | RefundPayment
        [Fact]
        public void ProcessPayment_NotCalled()
        {
            Assert.Equal(0, _paymentGatewayMockStubSpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.GetStatusCallCount);

            _paymentProcessor.RefundPayment("T-1");
            Assert.Equal(0, _paymentGatewayMockStubSpy.ChargeCallCount);
            Assert.Equal(1, _paymentGatewayMockStubSpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.GetStatusCallCount);


            _paymentProcessor.RefundPayment("T-1");
            Assert.Equal(0, _paymentGatewayMockStubSpy.ChargeCallCount);
            Assert.Equal(2, _paymentGatewayMockStubSpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.GetStatusCallCount);

            for (int i = 0; i < 10; i++)
            {
                _paymentProcessor.RefundPayment("T-1");
            }

            Assert.Equal(0, _paymentGatewayMockStubSpy.ChargeCallCount);
            Assert.Equal(12, _paymentGatewayMockStubSpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.GetStatusCallCount);
        }

        // Verification of the number of calls to individual methods. | GetPaymentStatus
        [Fact]
        public void GetPaymentStatus_CallCount()
        {
            Assert.Equal(0, _paymentGatewayMockStubSpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.RefundCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.GetStatusCallCount);

            _paymentProcessor.GetPaymentStatus("T-1");
            Assert.Equal(0, _paymentGatewayMockStubSpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.RefundCallCount);
            Assert.Equal(1, _paymentGatewayMockStubSpy.GetStatusCallCount);

            _paymentProcessor.GetPaymentStatus("T-1");
            Assert.Equal(0, _paymentGatewayMockStubSpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.RefundCallCount);
            Assert.Equal(2, _paymentGatewayMockStubSpy.GetStatusCallCount);

            for (int i = 0; i < 10; i++)
            {
                _paymentProcessor.GetPaymentStatus("T-1");
            }

            Assert.Equal(0, _paymentGatewayMockStubSpy.ChargeCallCount);
            Assert.Equal(0, _paymentGatewayMockStubSpy.RefundCallCount);
            Assert.Equal(12, _paymentGatewayMockStubSpy.GetStatusCallCount);
        }

    }
}