using System;

namespace dummy_project
{
    public class MyTestClass
    {
        public string MyProperty { get; set; }
        private int _myField;

        public MyTestClass()
        {
            MyProperty = "Hello";
            _myField = 123;
        }

        public void MyTestMethod(string param)
        {
            Console.WriteLine($"Parameter: {param}, Property: {MyProperty}, Field: {_myField}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World from dummy-project!");
            var myInstance = new MyTestClass();
            myInstance.MyTestMethod("Test Param");
        }
    }
}