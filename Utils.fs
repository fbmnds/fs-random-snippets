[<AutoOpen>]
module TestUtils
    open Microsoft.VisualStudio.TestTools.UnitTesting

    /// assert a = b
    let inline (==) a b = Assert.AreEqual(a,b)

    /// assert a <> b
    let inline (!=) a b = Assert.AreNotEqual(a,b)

    /// assert a and b are same: Assert.AreSame(a,b)
    let inline (===) a b = Assert.AreSame(a,b)

    /// assert a is TRUE, where + stands for TRUE
    let inline (?+) a = Assert.IsTrue(a)  //+ stands for TRUE

    /// assert b is FALSE, where - stands for FALSE
    let inline (?-) b = Assert.IsFalse(b) //- stands for FALSE

    /// assert source function and target function generates same sequence when given same input by using a compareFunction
    let inline agreeToBase compareFunction sourceFunction targetFunction input = 
        let source = input |> sourceFunction
        let target = input |> targetFunction
        let compareFunction a b = if compareFunction a b then 0 else -1
        let compareResult = Seq.compareWith compareFunction source target
        if compareResult <> 0 then failwith "sequence not equal"

    /// assert source function and target function generates same sequence when given same input
    let inline agreeTo sourceFunction targetFunction input = agreeToBase (=) sourceFunction targetFunction input

    /// not implement, always fail
    let inline notImplemented() = failwith "not implemented"; ()

    type Assert with

        /// assert source function and target function generates same sequence when given same input
        static member AgreeTo(sourceFunction, targetFunction, input) = 
            agreeTo sourceFunction targetFunction input

        /// assert source function and target function generates same sequence when given same input by using a compareFunction
        static member AgreeTo(compareFunction, sourceFunction, targetFunction, input) = 
            agreeToBase compareFunction sourceFunction targetFunction input 

        /// not implement, always fail
        static member NotImplemented() : unit= 
            failwith "not implemented"