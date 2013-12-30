// usage:
// 
// open EvolutionaryComputation
//
// let lociFunction() = box(random.Next(10))
// let fitnessF (items:obj list) = 
// items | > Seq.map (System.Convert.ToInt32) | > Seq.sum
//
// let myPopulation = Population lociFunction 50 10
//
// let myEvolve population = Evolve population RankSelection ShuffleCrossover fitnessF 0.9 0.1
//
// let result = composite myEvolve 75 myPopulation 
// 
// printfn "%A" (maxFitness result fitnessF)
// System.Console.ReadKey() |> ignore

/// Evoluationary computation module
/// this module is for Genetic Algorithm(GA) only
module EvolutionaryComputation

    /// random nubmer generator
    let random = new System.Random((int)System.DateTime.Now.Ticks)

    /// random int generator
    let randomF() = random.Next()

    /// random float (double) generator
    let randomFloatF() = random.NextDouble()

    /// Chromesome type to represent the individual involves in the GAs
    type ChromosomeType(f, size, ?converters) = 
        let initialF = f
        let mutable genos = [for i in 1..size do yield f()]
        let mutable genoPhenoConverters = converters

        /// make a duplicate copy of this chromosome
        member this.Clone() =
            let newValue = 
                match converters with
                    | Some(converters) -> new ChromosomeType(initialF, size, converters)
                    | None -> new ChromosomeType(initialF, size)
            newValue.Genos <- this.Genos 
            newValue 

        /// get fitness value with given fitness function
        member this.Fitness(fitnessFunction) = this.Pheno |> fitnessFunction

        /// gets and sets the Geno values
        member this.Genos
            with get() = genos 
            and set(value) = genos <- value 

        /// gets and sets the Pheno values
        member this.Pheno 
            with get() = 
                match genoPhenoConverters with 
                | Some(genoPhenoConverters) -> List.zip genoPhenoConverters genos |> List.map (fun (f,value) -> f value) 
                | None -> this.Genos

        /// mutate the chromosome with given mutation function
        member this.Mutate(?mutationF) =
            let location = random.Next(Seq.length this.Genos)
            let F = 
                match mutationF with
                    | Some(mutationF) -> mutationF
                    | None -> f
            let transform i v =
                match i with
                    | _ when i=location -> F()
                    | _ -> v
            this.Genos <- List.mapi transform this.Genos 

    /// generate a population for GAs
    let Population randomF populationSize chromosomeSize = 
        [for i in 1..populationSize do yield (new ChromosomeType(f=randomF,size=chromosomeSize))] 

    /// find the maximum fitness value from a population
    let maxFitness population fitnessF = 
        let best = Seq.maxBy (fun (c:ChromosomeType) -> c.Fitness(fitnessF)) population
        best.Fitness(fitnessF)

    /// find the most fit individual
    let bestChromosome population fitnessF = 
        let best = Seq.maxBy (fun (c:ChromosomeType) -> c.Fitness(fitnessF)) population
        best

    /// rank selection method
    let RankSelection (population:ChromosomeType list) fitnessFunction= 
        let populationSize = Seq.length population
        let r() = randomF() % populationSize
        let randomSelection() = 
            let c0 = population.[r()]
            let c1 = population.[r()]
            let result = if (c0.Fitness(fitnessFunction) > c1.Fitness(fitnessFunction)) then c0 else c1
            result.Clone();
        Seq.init populationSize (fun _ -> randomSelection())

    /// shuffle crossover
    let ShuffleCrossover (c0:ChromosomeType) (c1:ChromosomeType) =
        let crossover c0 c1 =
            let isEven n = n%2 = 0
            let randomSwitch (x,y) = if isEven (randomF()) then (x,y) else (y,x) 
            List.zip c0 c1 |> List.map randomSwitch |> List.unzip
        let (first,second) = crossover (c0.Genos) (c1.Genos)
        c0.Genos <- first 
        c1.Genos <- second
        
    /// evole the whole population 
    let Evolve (population:ChromosomeType list) selectionF crossoverF fitnessF crossoverRate mutationRate elitism = 
        let populationSize = Seq.length population 
        let r() = randomF() % populationSize 
        let elites = selectionF population fitnessF |> Seq.toList
        let seq0 = elites |> Seq.mapi (fun i element->(i,element)) |> Seq.filter (fun (i,_)->i%2=0) |> Seq.map (fun (_,b)->b)
        let seq1 = elites |> Seq.mapi (fun i element->(i,element)) |> Seq.filter (fun (i,_)->i%2<>0) |> Seq.map (fun (_,b)->b)
        let xoverAndMutate (a:ChromosomeType) (b:ChromosomeType) =
            if (randomFloatF() < crossoverRate) then 
                crossoverF a b 
            if (randomFloatF() < mutationRate) then 
                a.Mutate() 
            if (randomFloatF() < mutationRate) then 
                b.Mutate() 
            [a] @ [b] 

        if elitism then
            let seq0 = seq0
            let seq1 = seq1
            let r = Seq.map2 xoverAndMutate seq0 seq1 |> List.concat
            r.Tail @ [  bestChromosome population fitnessF ]
        else
            Seq.map2 xoverAndMutate seq0 seq1 |> List.concat

    /// composite funciton X times
    let rec composite f x = 
         match x with
            | 1 -> f
            | n -> f >> (composite f (x-1))

    /// convert a function seq to function composition
    let compositeFunctions functions = 
         Seq.fold ( >> ) id functions

