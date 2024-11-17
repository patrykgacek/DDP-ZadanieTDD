namespace DPP___Zadanie_TDD
{
    public enum TransactionStatus
    {
        PENDING,
        COMPLETED,
        FAILED
    }



    public class TransactionResult(bool success, string transactionId, string message = "")
    {
        public bool Success { get; set; } = success;
        public string TransactionId { get; set; } = transactionId;
        public string Message { get; set; } = message;
    }



    public interface IPaymentGateway
    {
        TransactionResult Charge(string userId, double amount);
        TransactionResult Refund(string transactionId);
        TransactionStatus GetStatus(string transactionId);
    }



    public class NetworkException(string message) : Exception(message) {}
    public class PaymentException(string message) : Exception(message) {}
    public class RefundException(string message) : Exception(message) {}


    public interface ILogger
    {
        void Log(string message);
    }


    public class Logger: ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }
    }


    public class PaymentProcessor(IPaymentGateway paymentGateway, ILogger logger)
    {
        private readonly IPaymentGateway _paymentGateway = paymentGateway;
        private readonly ILogger _logger = logger;

        public TransactionResult ProcessPayment(string userId, double amount)
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger.Log("userId not provided");
                return new TransactionResult(false, "", "");
            };
            if (amount <= 0)
            {
                _logger.Log("Amount negative or zero");
                return new TransactionResult(false, "", "");
            }

            try
            {
                var result = _paymentGateway.Charge(userId, amount);
                _logger.Log(result.Success ? "ProcessPayment successful" : "ProcessPayment failed");
                return result;
            }
            catch (NetworkException)
            {
                _logger.Log("ProcessPayment NetworkException");
                return new TransactionResult(false, "", "");
            }
            catch (PaymentException)
            {
                _logger.Log("ProcessPayment PaymentException");
                return new TransactionResult(false, "", "");
            }
        }

        public TransactionResult RefundPayment(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId))
            {
                _logger.Log("Transaction ID not provided");
                return new TransactionResult(false, "", "");
            }

            try
            {
                var result = _paymentGateway.Refund(transactionId);
                _logger.Log(result.Success ? "RefundPayment successful" : "RefundPayment failed");
                return result;
            }
            catch (NetworkException)
            {
                _logger.Log("RefundPayment NetworkException");
                return new TransactionResult(false, "", "");
            }
            catch (RefundException)
            {
                _logger.Log("RefundPayment PaymentException");
                return new TransactionResult(false, "", "");
            }
        }

        public TransactionStatus GetPaymentStatus(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId))
            {
                _logger.Log("Transaction ID not provided");
                return TransactionStatus.FAILED;
            }

            try
            {
                var status = _paymentGateway.GetStatus(transactionId);
                _logger.Log("GetPaymentStatus successful");
                return status;
            }
            catch (NetworkException)
            {
                _logger.Log("GetPaymentStatus NetworkException");
                return TransactionStatus.FAILED;
            }
        }
    }

}