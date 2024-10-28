using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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



    public class PaymentProcessor(IPaymentGateway paymentGateway)
    {
        private readonly IPaymentGateway _paymentGateway = paymentGateway;

        public TransactionResult ProcessPayment(string userId, double amount)
        {
            if (string.IsNullOrEmpty(userId)) return new TransactionResult(false, "", "Pusty user Id");
            if (amount <= 0) return new TransactionResult(false, "", "Amount <= 0");

            try
            {
                var result = _paymentGateway.Charge(userId, amount);
                Console.WriteLine(result.Success ? "ProcessPayment zakończony powodzeniem" : "ProcessPayment zakończony niepowodzeniem");
                return result;
            }
            catch (NetworkException ex)
            {
                Console.WriteLine("NetworkException: " + ex.Message);
                return new TransactionResult(false, "", "NetworkException: " + ex.Message);
            }
            catch (PaymentException ex)
            {
                Console.WriteLine("PaymentException: " + ex.Message);
                return new TransactionResult(false, "", "PaymentException: " + ex.Message);
            }
        }

        public TransactionResult RefundPayment(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId)) return new TransactionResult(false, "", "Pusty transactionId");

            try
            {
                var result = _paymentGateway.Refund(transactionId);
                Console.WriteLine(result.Success ? "RefundPayment zakończony powodzeniem" : "RefundPayment zakończony niepowodzeniem");
                return result;
            }
            catch (NetworkException ex)
            {
                Console.WriteLine("NetworkException: " + ex.Message);
                return new TransactionResult(false, "", "NetworkException: " + ex.Message);
            }
            catch (RefundException ex)
            {
                Console.WriteLine("RefundException: " + ex.Message);
                return new TransactionResult(false, "", "RefundException: " + ex.Message);
            }
        }

        public TransactionStatus GetPaymentStatus(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId)) throw new ArgumentException("Transaction ID cannot be empty.");

            try
            {
                return _paymentGateway.GetStatus(transactionId);
            }
            catch (NetworkException ex)
            {
                Console.WriteLine("NetworkException: " + ex.Message);
                return TransactionStatus.FAILED;
            }
        }
    }

}