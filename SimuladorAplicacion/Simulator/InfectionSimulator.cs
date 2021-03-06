﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Accord.Statistics.Distributions.Univariate;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.Layout.Incremental;
using Microsoft.Msagl.Layout.LargeGraphLayout;
using Microsoft.Msagl.Layout.MDS;
using Microsoft.Msagl.Prototype.Ranking;
using SimuladorAplicacion.Experimentation;
using SimuladorAplicacion.NetworkCreate;
using Node = Microsoft.Msagl.Core.Layout.Node;

namespace SimuladorAplicacion.Simulator
{

    class InfectionSimulator
    {
        private SimulatorParameters _parameters;

        private readonly Dictionary<string, SimulationHistory> networkHistories = new Dictionary<string, SimulationHistory>();

        private readonly Dictionary<string, Graph> InfectionVisualNetworks = new Dictionary<string, Graph>();

        private readonly Dictionary<string, Dictionary<string, InfectionNode>> networkNodes = new Dictionary<string, Dictionary<string, InfectionNode>>();

        public InfectionSimulator(SimulatorParameters parameters)
        {
            _parameters = parameters;
            //GetGraph1();
            //GetGraph2();

            GenerateSeededGraph("Red Simple", 20, 1024, 0.4);
            GenerateSeededGraph("Red Media", 50, 2048, 0.4);
            GenerateSeededGraph("Red Grande", 80, 48151623, 0.20);
            GenerateSeededGraph("Red Gigante", 120, 315234, 0.33);
            GenerateSeededGraph("Red Masiva", 160, 1245235, 0.33);
            GenerateSeededGraph("Red Gargantua", 200, 47109124, 0.33);
        }

        public void InfectRandomNode()
        {
            Random rand = new Random();

            var nodeInfected = rand.Next(0, InfectionVisualNetworks[_currentNetworkKey].NodeCount - 1);
            InfectNode(networkNodes[_currentNetworkKey].Keys.Skip(nodeInfected).First());
        }
        //Como es que tenes una property readonly que se devuelve por referencia y puede ser modificada
        public List<int> GetInfectionAmountHistory()
        {
            return networkHistories[_currentNetworkKey].InfectedPerIteration;
        }

        public int GetCurrentInfectedNodes()
        {
            return networkNodes[_currentNetworkKey].Values.Count(n => n.NodeStatus == NodeStatus.Infected);
        }

        public int GetHistoricInfectedNodes()
        {
            return networkHistories[_currentNetworkKey].HistoricalInfectedAmount;
        }

        public int GetHospitalizedAtRiskNodes()
        {
            return networkNodes[_currentNetworkKey].Values
                .Count(n => n.NodeStatus == NodeStatus.Hospitalized && n.NodeType == NodeType.AtRisk);
        }

        public int GetAmountIterations()
        {
            return networkHistories[_currentNetworkKey].AmountIterations;
        }

        public Graph RunIteration()
        {
            var infectionNodes = networkNodes[_currentNetworkKey];

            var rand = new Random(DateTime.Now.Millisecond);

            var previousStates = new Dictionary<string, InfectionNode>();

            foreach (var key in infectionNodes.Keys)
            {
                previousStates[key] = new InfectionNode(infectionNodes[key]);
            }

            foreach (var nodeKey in infectionNodes.Keys)
            {

                if (infectionNodes[nodeKey].NodeStatus == NodeStatus.Removed)
                {
                    MakeSuceptibleNode(nodeKey);
                }

                if (infectionNodes[nodeKey].NodeStatus == NodeStatus.Infected)
                {
                    var dayOfInfection = infectionNodes[nodeKey].DayOfInfection;
                    var resultadoAzar = rand.NextDouble();

                    var recoveryLikelyhood = new ChiSquareDistribution(_parameters.ChiSquaredRecoveryDistributionDegreesOfFreedom).DistributionFunction(dayOfInfection + 1);
                    
                    if (resultadoAzar <= recoveryLikelyhood)
                    {
                        RecoverNode(nodeKey);
                    }
                    else
                    {
                        WorsenNode(nodeKey);
                    }
                }

            }

            foreach (var edge in InfectionVisualNetworks[_currentNetworkKey].Edges)
            {
                var sourceKey = edge.SourceNode.Id;
                var destinationKey = edge.TargetNode.Id;

                if ((previousStates[sourceKey].NodeStatus == NodeStatus.Infected && previousStates[destinationKey].NodeStatus == NodeStatus.Susceptible))
                {
                    var resultadoAzar = rand.NextDouble();
                    //var infectionLikelyhood = _parameters.InfectionProbabilities[previousStates[destinationKey].DayOfInfection];
                    var infectionLikelyhood = new ChiSquareDistribution(_parameters.ChiSquaredDistributionDegreesOfFreedom).ProbabilityDensityFunction(previousStates[sourceKey].DayOfInfection + 1);
                    infectionLikelyhood = infectionLikelyhood * _parameters.InfectionChiSquaredFactor;

                    if (previousStates[destinationKey].NodeType == NodeType.Vaccinated)
                        infectionLikelyhood = infectionLikelyhood * _parameters.VaccineEfficiencyRatio;

                    if (resultadoAzar <= infectionLikelyhood)
                    {
                        InfectNode(destinationKey);
                    }
                }
                else if ((previousStates[destinationKey].NodeStatus == NodeStatus.Infected && previousStates[sourceKey].NodeStatus == NodeStatus.Susceptible))
                {
                    var resultadoAzar = rand.NextDouble();
                    //var infectionLikelyhood = _parameters.InfectionProbabilities[previousStates[destinationKey].DayOfInfection];
                    var infectionLikelyhood = new ChiSquareDistribution(_parameters.ChiSquaredDistributionDegreesOfFreedom).ProbabilityDensityFunction(previousStates[destinationKey].DayOfInfection + 1);
                    infectionLikelyhood = infectionLikelyhood * _parameters.InfectionChiSquaredFactor;

                    if (previousStates[sourceKey].NodeType == NodeType.Vaccinated)
                        infectionLikelyhood = infectionLikelyhood * _parameters.VaccineEfficiencyRatio;

                    if (resultadoAzar <= infectionLikelyhood)
                    {
                        InfectNode(sourceKey);
                    }
                }
            }

            networkHistories[_currentNetworkKey].InfectedPerIteration.Add(GetCurrentInfectedNodes());
            networkHistories[_currentNetworkKey].AmountIterations++;

            return InfectionVisualNetworks[_currentNetworkKey];
        }

        public Graph SetCurrentNetworkSimulation(string key)
        {
            if (InfectionVisualNetworks.ContainsKey(key))
            {
                _currentNetworkKey = key;
                return InfectionVisualNetworks[key];
            }

            throw new Exception("Non valid netwokr key requested");
        }

        public Graph GetCurrentGraph()
        {
            return InfectionVisualNetworks[_currentNetworkKey];
        }
        private string _currentNetworkKey = string.Empty;

        public void InfectNode(string nodeKey)
        {
            var node = networkNodes[_currentNetworkKey][nodeKey];
            if (node.NodeType == NodeType.AtRisk && node.NodeStatus != NodeStatus.Hospitalized)
            {
                InfectionVisualNetworks[_currentNetworkKey].FindNode(nodeKey).Attr.FillColor = Color.Black;
                networkNodes[_currentNetworkKey][nodeKey].NodeStatus = NodeStatus.Hospitalized;

                networkHistories[_currentNetworkKey].HistoricalInfectedAmount++;
                //TODO, HOSPITALIZADO = INFECTADO ?
            }
            else if (node.NodeStatus != NodeStatus.Infected)
            {

                InfectionVisualNetworks[_currentNetworkKey].FindNode(nodeKey).Attr.FillColor = Color.Red;
                networkNodes[_currentNetworkKey][nodeKey].NodeStatus = NodeStatus.Infected;

                networkHistories[_currentNetworkKey].HistoricalInfectedAmount++;

            }

        }

        private void RecoverNode(string nodeKey)
        {
            InfectionVisualNetworks[_currentNetworkKey].FindNode(nodeKey).Attr.FillColor = Color.Cyan;
            networkNodes[_currentNetworkKey][nodeKey].NodeStatus = NodeStatus.Removed;
            networkNodes[_currentNetworkKey][nodeKey].DayOfInfection = 0;
            networkNodes[_currentNetworkKey][nodeKey].DaysUntilSuceptible = _parameters.DaysOfImmunity;
        }

        private void MakeSuceptibleNode(string nodeKey)
        {
            networkNodes[_currentNetworkKey][nodeKey].DaysUntilSuceptible--;

            if (networkNodes[_currentNetworkKey][nodeKey].DaysUntilSuceptible <= 0)
            {
                var node = networkNodes[_currentNetworkKey][nodeKey];
                node.NodeStatus = NodeStatus.Susceptible;
                if (node.NodeType == NodeType.AtRisk)
                {
                    InfectionVisualNetworks[_currentNetworkKey].FindNode(nodeKey).Attr.FillColor = Color.Yellow;
                }
                else if (node.NodeType == NodeType.Vaccinated)
                {
                    InfectionVisualNetworks[_currentNetworkKey].FindNode(nodeKey).Attr.FillColor = Color.MediumPurple;
                }
                else
                {
                    InfectionVisualNetworks[_currentNetworkKey].FindNode(nodeKey).Attr.FillColor = Color.LightGreen;
                }
            }
        }

        private void WorsenNode(string nodeKey)
        {
            networkNodes[_currentNetworkKey][nodeKey].DayOfInfection++;

            //if (networkNodes[_currentNetworkKey][nodeKey].DaysInfected == _parameters.RecoveryProbabilities.Count)
            //{
            //    networkNodes[_currentNetworkKey][nodeKey].NodeStatus = NodeStatus.Hospitalized;
            //    InfectionVisualNetworks[_currentNetworkKey].FindNode(nodeKey).Attr.FillColor = Color.Black;
            //}
        }
        private Graph GenerateSeededGraph(string nombre, int size, uint seed, double conectividad)
        {
            uint w = 32;
            uint n = 624;
            uint m = 397;
            uint r = 31;
            uint a = 0x9908B0DF;
            uint u = 11;
            uint d = 0xFFFFFFFF;
            uint s = 7;
            uint b = 0x9D2C5680;
            uint t = 15;
            uint c = 0xEFC60000;
            uint I = 18;
            uint f = 1812433253;

            Graph grafo = new Graph(nombre);
            
            grafo.LayoutAlgorithmSettings = new RankingLayoutSettings();
            grafo.LayoutAlgorithmSettings = new MdsLayoutSettings();


            uint[] mT = new uint[n];
            uint indice = n + 1;
            uint mascara_inf = (uint) (1 << (int) r) - 1;
            uint mascara_sup = ~mascara_inf;

            indice = n;
            mT[0] = seed;

            for (uint i = 1; i < n; i++)
            {
                mT[i] = f * (mT[i - 1] ^ ((mT[i - 1] >> ((int)w - 2)) + i));
            }

            int edgeCounter = 0;
            while(edgeCounter < size * size)
            {
                if (indice >= n)
                {
                    if (indice > n)
                    {
                        throw new Exception("Nunca se seedeo el generador");
                    }

                    for (uint i = 0; i < n - 1; i++)
                    {
                        uint x = (mT[i] & mascara_sup) + ((mT[(i + 1)] % n) & mascara_inf);
                        uint xA = x >> 1;

                        if (x % 2 != 0)
                        {
                            xA = xA ^ a;
                        }
                        mT[i] = mT[(i + m) % n] ^ xA;
                    }

                    indice = 0;
                }

                uint y = mT[indice];

                y = y ^ ((y >> (int)u) & d);
                y = y ^ ((y << (int)s) & b);
                y = y ^ ((y << (int)t) & c);
                y = y ^ ((y >> 1));

                indice++;

                float floatSeedead0 = (float)y / uint.MaxValue;
                //Fin MSTR

                int nodoOrigen = edgeCounter / size;
                int nodoDest = edgeCounter % size;

                if (nodoOrigen == nodoDest)
                {
                    edgeCounter++;
                    continue;
                }

                double probUniforme = (double) (size - Math.Abs(nodoOrigen - nodoDest)) / (Math.Pow(size, (double) 2 - conectividad));

                if (floatSeedead0 < probUniforme)
                {
                    if (!grafo.Edges.Any(e => e.Target == nodoOrigen.ToString() && e.Source == nodoDest.ToString()))
                        grafo.AddEdge(nodoOrigen.ToString(), nodoDest.ToString()).Attr.ArrowheadAtTarget = ArrowStyle.None;
                }

                edgeCounter++;
            }

            var infectionNodes = CreateDataAndColorize(grafo);

            //bind the graph to the viewer 
            networkNodes.Add(grafo.Label.Text, infectionNodes);
            InfectionVisualNetworks.Add(grafo.Label.Text, grafo);
            networkHistories.Add(grafo.Label.Text, new SimulationHistory());

            return grafo;

        }

        private void GetGraph2()
        {
            var graph = new Graph("Red Media");

            graph.LayoutAlgorithmSettings = new RankingLayoutSettings();
            graph.LayoutAlgorithmSettings = new MdsLayoutSettings();

            //graph.LayoutAlgorithmSettings = new FastIncrementalLayoutSettings();
            //create the graph content 
            graph.AddEdge("1", "2").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("1", "3").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("1", "6").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("2", "3").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("2", "15").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("3", "4").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("3", "7").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("4", "5").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("4", "6").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("4", "7").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("5", "6").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("5", "7").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("5", "39").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("6", "24").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("7", "8").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("7", "12").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("7", "15").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("8", "9").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("8", "11").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("8", "12").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("9", "10").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("9", "16").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("10", "11").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("10", "14").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("10", "15").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("11", "14").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("11", "15").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("12", "14").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("12", "15").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("12", "19").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("13", "19").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("13", "24").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("14", "15").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("14", "18").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("14", "23").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("15", "16").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("15", "17").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("16", "17").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("17", "25").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("18", "19").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("18", "20").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("18", "21").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("19", "21").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("19", "24").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("20", "21").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("20", "23").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("21", "22").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("21", "23").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("21", "33").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("22", "23").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("22", "24").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("22", "44").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("23", "24").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("23", "25").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("24", "25").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("24", "26").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("24", "28").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("25", "26").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("25", "27").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("25", "28").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("25", "31").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("26", "27").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("26", "30").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("27", "28").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("27", "29").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("29", "30").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("29", "37").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("29", "42").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("30", "31").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("30", "36").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("30", "37").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("30", "38").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("31", "32").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("31", "35").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("31", "36").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("32", "33").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("32", "34").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("32", "35").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("32", "43").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("33", "34").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("33", "43").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("34", "43").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("34", "44").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("35", "41").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("35", "42").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("36", "38").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("36", "41").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("37", "39").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("37", "40").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("38", "39").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("39", "40").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("40", "41").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("40", "43").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("41", "42").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("41", "43").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("42", "44").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("43", "44").Attr.ArrowheadAtTarget = ArrowStyle.None;

            var infectionNodes = CreateDataAndColorize(graph);

            //bind the graph to the viewer 
            networkNodes.Add(graph.Label.Text, infectionNodes);
            InfectionVisualNetworks.Add(graph.Label.Text, graph);
            networkHistories.Add(graph.Label.Text, new SimulationHistory());
        }

        public void SetParameters(SimulatorParameters parameters)
        {
            _parameters = parameters;
        }

        internal void CreateNewNetwork(InfectionNetworkParameters parameters)
        {
            double factorDecimal = parameters.ConnectivityFactor - 1;
            double factorDe0a1 = factorDecimal / 9;

            GenerateSeededGraph(parameters.Name, parameters.NodeQuantity, parameters.Seed, factorDe0a1);
        }

        private void GetGraph1()
        {
            var graph = new Graph("Red Simple");

            graph.LayoutAlgorithmSettings = new RankingLayoutSettings();
            graph.LayoutAlgorithmSettings = new MdsLayoutSettings();

            //graph.LayoutAlgorithmSettings = new FastIncrementalLayoutSettings();
            //create the graph content 
            graph.AddEdge("A", "B").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("A", "C").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("B", "C").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("B", "D").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("B", "L").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("C", "G").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("C", "H").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("C", "F").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("D", "E").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("D", "L").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("D", "N").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("E", "F").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("E", "M").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("F", "L").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("F", "J").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("G", "H").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("G", "Ñ").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("G", "R").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("I", "J").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("I", "K").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("I", "S").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("K", "N").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("K", "L").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("L", "M").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("M", "N").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("Ñ", "O").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("O", "P").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("O", "R").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("P", "Q").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("Q", "T").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("Q", "W").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("R", "T").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("R", "S").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("R", "U").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("S", "U").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("S", "V").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("T", "V").Attr.ArrowheadAtTarget = ArrowStyle.None;
            graph.AddEdge("T", "W").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("U", "V").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("V", "X").Attr.ArrowheadAtTarget = ArrowStyle.None;

            graph.AddEdge("W", "X").Attr.ArrowheadAtTarget = ArrowStyle.None;
            //graph.AddEdge("M", "N").Attr.Color = Microsoft.Msagl.Drawing.Color.Green;

            var infectionNodes = CreateDataAndColorize(graph);


            //bind the graph to the viewer 
            networkNodes.Add(graph.Label.Text, infectionNodes);
            InfectionVisualNetworks.Add(graph.Label.Text, graph);
            networkHistories.Add(graph.Label.Text, new SimulationHistory());

        }

        public void ResetSimulation()
        {
            var infectionNodes = CreateDataAndColorize(InfectionVisualNetworks[_currentNetworkKey]);
            
            networkNodes[_currentNetworkKey] = infectionNodes;
            
            networkHistories[_currentNetworkKey] = new SimulationHistory();
        }

        private Dictionary<string, InfectionNode> CreateDataAndColorize(Graph graph)
        {
            var infectionNodes = new Dictionary<string, InfectionNode>();

            double amountOfVaccinates = graph.NodeCount * _parameters.VaccinatedRatio;
            Random rand = new Random(DateTime.Now.Millisecond);

            foreach (var node in graph.Nodes)
            {
                infectionNodes.Add(node.Id, new InfectionNode()
                {
                    NodeStatus = NodeStatus.Susceptible,
                    NodeType = NodeType.Healthy,
                });
            }

            for (int i = 0; i < amountOfVaccinates; i++)
            {
                var nodeVaccinated = rand.Next(0, graph.NodeCount - 1);

                while (infectionNodes[infectionNodes.Keys.Skip(nodeVaccinated).First()].NodeType != NodeType.Healthy)
                {
                    nodeVaccinated = rand.Next(0, graph.NodeCount - 1);
                }

                infectionNodes[infectionNodes.Keys.Skip(nodeVaccinated).First()].NodeType = NodeType.Vaccinated;

            }

            double amountOfAtRisk = graph.NodeCount * _parameters.AtRiskRatio;
            for (int i = 0; i < amountOfAtRisk; i++)
            {
                var nodeVaccinated = rand.Next(0, graph.NodeCount - 1);

                while (infectionNodes[infectionNodes.Keys.Skip(nodeVaccinated).First()].NodeType != NodeType.Healthy &&
                    infectionNodes[infectionNodes.Keys.Skip(nodeVaccinated).First()].NodeType != NodeType.Vaccinated)
                {
                    nodeVaccinated = rand.Next(0, graph.NodeCount - 1);
                }

                infectionNodes[infectionNodes.Keys.Skip(nodeVaccinated).First()].NodeType = NodeType.AtRisk;

            }


            foreach (var node in graph.Nodes)
            {
                node.Attr.Shape = Shape.Circle;

                if (infectionNodes[node.Id].NodeType == NodeType.Vaccinated)
                {
                    node.Attr.FillColor = Color.MediumPurple;
                }
                else if (infectionNodes[node.Id].NodeType == NodeType.AtRisk)
                {
                    node.Attr.FillColor = Color.Yellow;
                }
                else
                {
                    node.Attr.FillColor = Color.PaleGreen;
                }

            }

            foreach (var edge in graph.Edges)
            {
                edge.Attr.ArrowheadLength = 0;
            }

            return infectionNodes;
        }

    }
}
