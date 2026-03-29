using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Banganka.Core.Config;

namespace Banganka.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for ErrorHandler — requires MonoBehaviour for coroutine-based RetryWithBackoff.
    /// </summary>
    [TestFixture]
    public class ErrorHandlerTests
    {
        GameObject _go;
        ErrorHandler _handler;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ErrorHandler_Test");
            _handler = _go.AddComponent<ErrorHandler>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.Destroy(_go);
        }

        // ================================================================
        // Singleton
        // ================================================================

        [UnityTest]
        public IEnumerator Singleton_IsSetAfterAwake()
        {
            yield return null;
            Assert.IsNotNull(ErrorHandler.Instance);
            Assert.AreEqual(_handler, ErrorHandler.Instance);
        }

        [UnityTest]
        public IEnumerator Singleton_SecondInstance_IsDestroyed()
        {
            yield return null;

            var go2 = new GameObject("ErrorHandler_Dup");
            var dup = go2.AddComponent<ErrorHandler>();
            yield return null;

            // Original should remain
            Assert.AreEqual(_handler, ErrorHandler.Instance);
            // Duplicate should be destroyed (or queued for destruction)
            yield return null;
            Assert.IsTrue(dup == null, "Duplicate ErrorHandler should be destroyed");

            if (go2 != null) UnityEngine.Object.Destroy(go2);
        }

        // ================================================================
        // HandleError + event
        // ================================================================

        [UnityTest]
        public IEnumerator HandleError_FiresOnErrorDisplayed()
        {
            yield return null;

            string receivedTitle = null;
            string receivedMsg = null;
            _handler.OnErrorDisplayed += (title, msg) =>
            {
                receivedTitle = title;
                receivedMsg = msg;
            };

            _handler.HandleError(ErrorHandler.ErrorCategory.Network, "test timeout");
            yield return null;

            Assert.AreEqual("通信エラー", receivedTitle);
            Assert.IsNotNull(receivedMsg);
            Assert.IsTrue(receivedMsg.Contains("通信"));
        }

        [UnityTest]
        public IEnumerator HandleError_AllCategories_ProduceUserMessage()
        {
            yield return null;

            var categories = (ErrorHandler.ErrorCategory[])
                Enum.GetValues(typeof(ErrorHandler.ErrorCategory));

            foreach (var cat in categories)
            {
                string title = null;
                string msg = null;
                void handler(string t, string m) { title = t; msg = m; }

                _handler.OnErrorDisplayed += handler;
                _handler.HandleError(cat, $"test_{cat}");
                _handler.OnErrorDisplayed -= handler;

                Assert.IsNotNull(title, $"Category {cat} should produce a title");
                Assert.IsNotNull(msg, $"Category {cat} should produce a user message");
            }

            yield return null;
        }

        // ================================================================
        // RetryWithBackoff
        // ================================================================

        [UnityTest]
        public IEnumerator RetryWithBackoff_SucceedsOnFirstTry()
        {
            yield return null;

            int callCount = 0;
            ErrorHandler.RetryWithBackoff(() => { callCount++; }, maxRetries: 3, baseDelay: 0.1f);

            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(1, callCount, "Should call action once on success");
        }

        [UnityTest]
        public IEnumerator RetryWithBackoff_RetriesOnFailure()
        {
            yield return null;

            int callCount = 0;
            ErrorHandler.RetryWithBackoff(() =>
            {
                callCount++;
                if (callCount < 3) throw new Exception("transient");
            }, maxRetries: 3, baseDelay: 0.1f);

            // Wait enough for retries: 0.1s + 0.2s + margin
            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(3, callCount, "Should retry until success");
        }

        [UnityTest]
        public IEnumerator RetryWithBackoff_ExhaustsRetries_CallsHandleError()
        {
            yield return null;

            bool errorHandled = false;
            _handler.OnErrorDisplayed += (_, _) => errorHandled = true;

            int callCount = 0;
            ErrorHandler.RetryWithBackoff(() =>
            {
                callCount++;
                throw new Exception("permanent");
            }, maxRetries: 2, baseDelay: 0.1f);

            // Wait for all retries: 0.1s + 0.2s + margin
            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(3, callCount, "Should try 1 + 2 retries = 3 total");
            Assert.IsTrue(errorHandled, "Should call HandleError after exhausting retries");
        }
    }
}
