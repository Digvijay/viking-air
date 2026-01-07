using Xunit;
using Skugga.Core;
using VikingAir.Core;

namespace VikingAir.Tests;

public class BookingTests
{
    [Fact]
    public void Payment_ShouldSucceed_ForVisaCards()
    {
        // SKUGGA v1.1.0 DEMO
        // ------------------
        // This runs in Native AOT because it uses Interceptors, not Reflection.
        
        // 1. Create the mock (Compile-time generation)
        var gatewayMock = Mock.Create<IPaymentGateway>();

        // 2. Setup with Advanced Matchers (v1.3.0 Feature)
        // Using Regex to match only Visa cards starting with 4
        gatewayMock.Setup(x => x.Charge(It.IsRegex("^4[0-9]{12}(?:[0-9]{3})?$"), It.IsAny<decimal>()))
                   .Returns(true);

        // 3. Act
        // This matches the regex
        var resultSuccess = gatewayMock.Charge("4000123456789010", 100.00m);
        // This fails the regex
        var resultFail = gatewayMock.Charge("5000123456789010", 100.00m);

        // 4. Assert
        Assert.True(resultSuccess);
        Assert.False(resultFail);
    }
}
