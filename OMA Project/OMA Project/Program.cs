﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;
using OMA_Project.Extensions;
using System;
using System.Linq;

namespace OMA_Project
{
    internal static class Program
    {
        public static List<int> totalUsers = new List<int>();
        public static void Main(string[] args)
        {
            Problem x = Problem.ReadFromFile(@"C:\Users\Fylax\Desktop\Material_assignment\input\Co_30_1_NT_0.txt");
            using (Timer r = new Timer(4850))
            {
                Stopwatch s = Stopwatch.StartNew();
                r.Elapsed += Callback;
                r.Enabled = true;
                LinkedList<int[]> currentSolution = Solver.GreedySolution(x);
                LinkedList<int[]> bestSolution = currentSolution.DeepClone();
                int bestFitness = Solver.ObjectiveFunction(currentSolution, x.Matrix);

                int iterations = 0;
                int exponent = 0;
                const int plateauLength = 15;
                const double alpha = 0.8;
                const int T0 = 1000;
                ulong counter = 0;
                while (r.Enabled)
                {
                    counter++;
                    int tempFitness = Solver.SimulatedAnnealing(ref currentSolution, x, Math.Pow(alpha, exponent) * T0);
                    if (tempFitness < bestFitness)
                    {
                        bestSolution = currentSolution.DeepClone();
                        bestFitness = tempFitness;
                    }
                    if (++iterations % plateauLength == 0)
                    {
                        ++exponent;
                        iterations = 0;
                    }
                }
                s.Stop();
                WriteSolution.Write(args[1], bestSolution, bestFitness, s.ElapsedMilliseconds, args[0]);
            }
        }

        private static void Callback(object sender, ElapsedEventArgs e)
        {
            ((Timer)sender).Enabled = false;
        }
    }
}
