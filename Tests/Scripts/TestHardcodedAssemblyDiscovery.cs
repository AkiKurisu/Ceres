using System;
using System.Linq;
using Ceres.Graph.Flow;
using UnityEngine;

namespace Ceres.Tests
{
    public class TestHardcodedAssemblyDiscovery : MonoBehaviour
    {
        [ContextMenu("Test Hardcoded Assembly Discovery")]
        public void TestDiscovery()
        {
            try
            {
                var reflection = ExecutableReflection<UnityEngine.Debug>.GetFunction(
                    ExecutableFunctionType.StaticMethod, "Log", 1);
                
                if (reflection != null)
                {
                    Debug.Log("SUCCESS: Found UnityEngine.Debug.Log method from hardcoded assembly discovery!");
                }
                else
                {
                    Debug.LogWarning("FAILED: Could not find UnityEngine.Debug.Log method");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"ERROR during hardcoded assembly discovery test: {ex.Message}");
            }

            try
            {
                var mathfReflection = ExecutableReflection<UnityEngine.Mathf>.GetFunction(
                    ExecutableFunctionType.StaticMethod, "Abs", 1);
                
                if (mathfReflection != null)
                {
                    Debug.Log("SUCCESS: Found UnityEngine.Mathf.Abs method from hardcoded assembly discovery!");
                }
                else
                {
                    Debug.LogWarning("FAILED: Could not find UnityEngine.Mathf.Abs method");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"ERROR during Mathf discovery test: {ex.Message}");
            }
        }
    }
}
