﻿using System;
using System.Collections.Generic;
using System.Linq;
using OMA_Project.Extensions;
using static OMA_Project.Program;

namespace OMA_Project
{
    /// <summary>
    ///     Static class that exposes all the methods to profide feasible solution (if any)
    ///     and try to obtain the optimal one.
    /// </summary>
    public static class Solver
    {
        /// <summary>
        ///     Computes the initial solution, generated
        ///     through the Change Making Problem.
        /// </summary>
        /// <returns>
        ///     Initial solution in a flattened array,
        ///     each tuple is 6-element long, with:
        ///     <list type="number">
        ///         <item>Start</item>
        ///         <item>Destination</item>
        ///         <item>Time slot</item>
        ///         <item>User type</item>
        ///         <item>Required users</item>
        ///         <item>Performed tasks</item>
        ///     </list>
        /// </returns>
        public static List<int> InitialSolution()
        {
            var solution = new List<int>(1000);
            var orderedTask = problem.Tasks.Select((t, c) => new { cell = c, task = t })
                .Where(t => t.task != 0).OrderBy(t => t.task).ToArray();
            var totalUsers = problem.TotalUsers();
            for (var i = orderedTask.Length; i-- > 0;)
                SolvePreciseTasks(solution, totalUsers, orderedTask[i].cell, orderedTask[i].task);
            return solution;
        }

        /// <summary>
        ///     Solves tasks in a deterministic way (through the Change Making Problem)
        /// </summary>
        /// <param name="movings">Current solution which will be updated</param>
        /// <param name="totUsers">Total of available user for each type</param>
        /// <param name="destination">Requested destination</param>
        /// <param name="tasks">Tasks that must to be accomplished</param>
        private static void SolvePreciseTasks(List<int> movings, int[] totUsers, int destination, int tasks)
        {
            // Optimization block (not really required, just more readability ed enforced inling)
            var costs = problem.Matrix;
            var tasksPerUser = problem.TasksPerUser;
            var availability = problem.Availability;
            var timeSlots = problem.TimeSlots;
            var userTypes = problem.UserTypes;
            // end optimization block;
            var usable = new bool[userTypes];
            for (var i = totUsers.Length; i-- > 0;)
                if (totUsers[i] != 0)
                    usable[i] = true;
            var partitioned = OptimizeSolving(tasks, usable);
            for (var i = partitioned.Length; i-- > 0;)
                while (partitioned[i] != 0)
                {
                    var minimum = costs.GetMin(destination, i);
                    var available =
                        availability[(minimum[0] * timeSlots + minimum[1]) * userTypes + i];
                    if (available >= partitioned[i])
                    {
                        var doneTasks = tasks < tasksPerUser[i].Tasks * partitioned[i]
                            ? tasks
                            : partitioned[i] * tasksPerUser[i].Tasks;
                        tasks -= partitioned[i] * tasksPerUser[i].Tasks;
                        availability[(minimum[0] * timeSlots + minimum[1]) * userTypes + i] -=
                            partitioned[i];
                        problem.Users -= partitioned[i];
                        totUsers[i] -= partitioned[i];
                        movings.Add(minimum[0]); //start
                        movings.Add(destination); //destination
                        movings.Add(minimum[1]); //timeslot
                        movings.Add(i); //usertype
                        movings.Add(partitioned[i]); //usernumber
                        movings.Add(doneTasks); //perfomed tasks
                        movings.Add(0); //probability to be removed
                        movings.Add(0); //saturation field
                        partitioned[i] = 0;
                    }
                    else
                    {
                        var doneTasks = tasks < tasksPerUser[i].Tasks * partitioned[i]
                            ? tasks
                            : available * tasksPerUser[i].Tasks;
                        tasks -= available * tasksPerUser[i].Tasks;
                        partitioned[i] -= available;
                        availability[(minimum[0] * timeSlots + minimum[1]) * userTypes + i] -=
                            available;
                        problem.Users -= available;
                        totUsers[i] -= available;
                        movings.Add(minimum[0]); //start
                        movings.Add(destination); //destination
                        movings.Add(minimum[1]); //timeslot
                        movings.Add(i); //usertype
                        movings.Add(available); //usernumber
                        movings.Add(doneTasks); //perfomed tasks
                        movings.Add(0); //probability to be removed
                        movings.Add(0); //saturation field
                    }
                    if (totUsers[i] == 0)
                    {
                        usable[i] = false;
                        partitioned = OptimizeSolving(tasks, usable);
                        i = partitioned.Length - 1;
                    }
                }
        }

        /// <summary>
        ///     Implementation of Change Making Problem (modified version).
        ///     <para />
        ///     Differently from the original problem it both minimizes required users
        ///     and eventual overbooking (difference between performable task by an user
        ///     and actually performed tasks).
        ///     <para />
        ///     Futhermore it takes into account availability of users, skipping the ones that
        ///     aren't available.
        /// </summary>
        /// <param name="tasks">Tasks that must to be splitted between users</param>
        /// <param name="usable">
        ///     Wheter users of each type are usable or not (which
        ///     can be traslated in "there are user left of this type)
        /// </param>
        /// <returns>Array containing required users for each type.</returns>
        private static int[] OptimizeSolving(int tasks, bool[] usable)
        {
            // Optimization block (not really required, just more readability ed enforced inling)
            var tasksPerUser = problem.TasksPerUser;
            var userTypes = problem.UserTypes;
            // end optimization block
            var d = new int[userTypes];
            for (var i = userTypes; i-- > 0;)
                d[i] = tasksPerUser[i].Tasks;

            var user = 0;
            var c = new int[tasks + 1];
            var s = new int[tasks + 1];

            for (var p = 1; p <= tasks; p++)
            {
                var min = int.MaxValue;
                var overBooking = int.MinValue;
                for (var j = 0; j < d.Length; j++)
                    if (d[j] - d[0] < p && usable[j])
                    {
                        int tempMin;
                        int tempUser;
                        if (p - d[j] < 0)
                        {
                            tempMin = 1;
                            tempUser = tasksPerUser[j].UserType;
                        }

                        else if (1 + c[p - d[j]] < min)
                        {
                            tempMin = 1 + c[p - d[j]];
                            tempUser = tasksPerUser[j].UserType;
                        }

                        else
                        {
                            break;
                        }
                        var neededUsers = UsersNeeded(p, s);
                        var tempOverBooking = p;
                        for (var z = userTypes; z-- > 0;)
                            tempOverBooking -= neededUsers[z] * tasksPerUser[z].Tasks;
                        if (tempOverBooking < overBooking) continue;
                        min = tempMin;
                        user = tempUser;
                        overBooking = tempOverBooking;
                    }
                c[p] = min;
                s[p] = user;
            }
            return UsersNeeded(tasks, s);
        }

        /// <summary>
        ///     Computes the user required to partition a task.
        /// </summary>
        /// <param name="tasks">Tasks to be splitted</param>
        /// <param name="s">s vector of Change Making Problem</param>
        /// <returns>Number of users required for each type</returns>
        private static int[] UsersNeeded(int tasks, int[] s)
        {
            var tasksPerUser = problem.TasksPerUser; // just optimization
            var returns = new int[problem.UserTypes];
            for (var i = tasks; i > 0;)
            {
                returns[s[i]]++;
                i -= tasksPerUser[s[i]].Tasks;
            }
            return returns;
        }

        /// <summary>
        ///     Solves tasks in a greedy way (always take the minimum cost bestween the
        ///     available users).
        /// </summary>
        /// <param name="movings">Current solution that will be updated</param>
        /// <param name="destination">Requested destination</param>
        /// <exception cref="NoUserLeft">
        ///     Underlying greedy used all available users
        ///     while it need more of them.
        /// </exception>
        private static void SolveTasks(List<int> movings, int destination)
        {
            // Optimization block (not really required, just more readability ed enforced inling)
            var costs = problem.Matrix;
            var tasksPerUser = problem.TasksPerUser;
            var availability = problem.Availability;
            var timeSlots = problem.TimeSlots;
            var userTypes = problem.UserTypes;
            // end optimization block;
            var tasks = problem.Tasks[destination];
            var lookup = new HashSet<int>();
            var droppable = new List<int>();
            for (var i = movings.Count; (i -= 8) >= 0;)
                if (movings[i + 1] == destination)
                {
                    lookup.Add(i);
                    tasks -= movings[i + 5];
                }
            while (tasks != 0)
            {
                if (problem.Users == 0)
                    throw new NoUserLeft();
                var minimum = costs.GetMin(destination);
                var available =
                    availability[(minimum[0] * timeSlots + minimum[1]) * userTypes + minimum[2]];
                int used;
                int performedTasks;
                if (available * tasksPerUser[minimum[2]].Tasks >= tasks)
                {
                    // shift based ceiling function (way faster than Math.Ceiling)
                    used = 32768 - (int)(32768d - tasks / (double)tasksPerUser[minimum[2]].Tasks);
                    performedTasks = tasks;
                    tasks = 0;
                }
                else
                {
                    used = available;
                    tasks -= used * tasksPerUser[minimum[2]].Tasks;
                    performedTasks = used * tasksPerUser[minimum[2]].Tasks;
                }
                availability[(minimum[0] * timeSlots + minimum[1]) * userTypes + minimum[2]] -= used;
                problem.Users -= used;
                var currentPtr = -1;
                //compactizator
                foreach (var ptr in lookup)
                    if (movings[ptr] == minimum[0] && movings[ptr + 2] == minimum[1] &&
                        movings[ptr + 3] == minimum[2])
                    {
                        movings[ptr + 4] += used;
                        movings[ptr + 5] += performedTasks;
                        currentPtr = ptr;
                        var overbooking = movings[ptr + 4] * problem.TasksPerUser[movings[ptr + 3]].Tasks -
                                          movings[ptr + 5];
                        if (overbooking >= problem.TasksPerUser[movings[ptr + 3]].Tasks)
                        {
                            var toBeRemoved = overbooking / problem.TasksPerUser[movings[ptr + 3]].Tasks;
                            movings[ptr + 4] -= toBeRemoved;
                            problem.Availability[
                                (movings[ptr] * problem.TimeSlots + movings[ptr + 2]) * problem.UserTypes +
                                movings[ptr + 3]] += toBeRemoved;
                            problem.Users += toBeRemoved;
                        }
                        break;
                    }
                if (currentPtr == -1)
                {
                    currentPtr = movings.Count;
                    movings.Add(minimum[0]);
                    movings.Add(destination);
                    movings.Add(minimum[1]);
                    movings.Add(minimum[2]);
                    movings.Add(used);
                    movings.Add(performedTasks);
                    movings.Add(0);
                    movings.Add(0);
                }

                // sweeper!
                var overBooking = used * tasksPerUser[minimum[2]].Tasks - movings[currentPtr + 5];
                for (var i = userTypes; i-- > 0 && overBooking != 0;)
                    foreach (var ptr in lookup)
                    {
                        if (ptr == currentPtr) continue;
                        if (overBooking == 0) break;
                        if (movings[ptr + 3] != i) continue;
                        var lastPerformed = tasksPerUser[i].Tasks * (1 - movings[ptr + 4]) + movings[ptr + 5];
                        if (lastPerformed > overBooking) continue;
                        overBooking -= lastPerformed;
                        movings[ptr + 4]--;
                        movings[currentPtr + 5] += lastPerformed;
                        availability[(movings[ptr] * timeSlots + movings[ptr + 2]) * userTypes + movings[ptr + 3]]++;
                        problem.Users++;
                        if (movings[ptr + 4] != 0)
                            movings[ptr + 5] -= lastPerformed;
                        else
                            droppable.Add(ptr);
                    }

                foreach (var ptr in droppable.OrderByDescending(s => s))
                    movings.RemoveRange(ptr, 8);
            }
        }

        /// <summary>
        ///     Computes the objective function.
        /// </summary>
        /// <param name="solution"></param>
        /// <returns>Value of objective function</returns>
        public static int ObjectiveFunction(List<int> solution)
        {
            // Optimization block (not really required, just more readability ed enforced inling)
            var costs = problem.Matrix;
            var cells = problem.Cells;
            var timeSlots = problem.TimeSlots;
            var userTypes = problem.UserTypes;
            // end optimization block;
            int sum;
            int j;
            if (solution.Count / 8 % 2 != 0)
            {
                sum = costs.costMatrix[
                          ((solution[0] * cells + solution[1]) * timeSlots + solution[2]) * userTypes +
                          solution[3]] * solution[4];
                j = 8;
            }
            else
            {
                sum = 0;
                j = 0;
            }
            var times = solution.Count / 2;
            for (var i = solution.Count; (i -= 8) >= times; j += 8)
                if (i == j)
                    sum = sum +
                          costs.costMatrix[
                              ((solution[i] * cells + solution[i + 1]) * timeSlots + solution[i + 2]) * userTypes +
                              solution[i + 3]] * solution[i + 4];
                else
                    sum = sum +
                          costs.costMatrix[
                              ((solution[i] * cells + solution[i + 1]) * timeSlots + solution[i + 2]) * userTypes +
                              solution[i + 3]] * solution[i + 4] +
                          costs.costMatrix[
                              ((solution[j] * cells + solution[j + 1]) * timeSlots + solution[j + 2]) * userTypes +
                              solution[j + 3]] * solution[j + 4];
            return sum;
        }

        /// <summary>
        ///     Computes the Variable Neighborhood Search Metaheuristic.
        ///     <para />
        ///     Neighborhood is generated by dropping some random tuples from the
        ///     solution table and then recompute them through a greedy.
        ///     <seealso cref="SolveTasks" />
        /// </summary>
        /// <param name="movings">Current solution from which tuples will be dropped.</param>
        /// <param name="history">History of dropped tuples.</param>
        /// <param name="percentage">Percentage of tuples to be dropped (as integer). This means that 1 is 1%.</param>
        /// <param name="bestFitness">Actual best found fitness.</param>
        /// <returns>
        ///     Solution processed by the VNS.
        /// </returns>
        public static Tuple<List<int>, int> VNS(List<int> movings, List<int> history, int percentage, int bestFitness)
        {
            const int SaturationBoundary = 100;
            var selectable = 0;
            var numTuples = movings.Count / 8;
            var counter = numTuples * percentage / 100;
            /*
             * Even though destinationSelected is easly computable through droppedTuples, 
             * it is useful to avoid solving tasks (computing them and noticing are zero)
             * for same destination multiple times (avoids duplication)
             */
            var destinationSelected = new HashSet<int>(); 
            var droppedIndices = new bool[numTuples];
            List<int> droppedTuples = new List<int>(movings.Capacity);

            for (var j = movings.Count; (j -= 8) >= 0;)
            {
                if (movings[j + 7] == 1) continue;
                selectable++;
            }
            //counter = selectable*percentage/100; //non sono d'accordo con questa cosa, così si rompe il VNS praticamente
            if (selectable < counter)
            {
                if (selectable == 0)
                {
                    return new Tuple<List<int>, int>(movings, ObjectiveFunction(movings));
                }
                counter = selectable;
            }
            var currentSolution = movings.DeepClone();
            for (var i = counter; i-- > 0;)
            {
                int droppedIndex;
                do
                {
                    bool drop;
                    do
                    {
                        droppedIndex = generator.Next(numTuples);
                        drop = currentSolution[droppedIndex * 8 + 7] != 1;
                        if (drop)
                            drop = currentSolution[droppedIndex * 8 + 6] <= generator.Next(1, SaturationBoundary);
                    } while (!drop);
                } while (droppedIndices[droppedIndex]);
                
       
                droppedIndices[droppedIndex] = true;
                droppedIndex *= 8;

                problem.Availability[
                    (currentSolution[droppedIndex] * problem.TimeSlots + currentSolution[droppedIndex + 2]) * problem.UserTypes +
                    currentSolution[droppedIndex + 3]] += currentSolution[droppedIndex + 4];
                problem.Users += currentSolution[droppedIndex + 4];
                destinationSelected.Add(currentSolution[droppedIndex + 1]);        // destination
            }
            
            var tempList = new List<int>(currentSolution.Capacity);
            for (var i = 0; i < currentSolution.Count; i += 8)
            {
                if (droppedIndices[i / 8])
                {
                    droppedTuples.Add(currentSolution[i]);
                    droppedTuples.Add(currentSolution[i + 1]);
                    droppedTuples.Add(currentSolution[i + 2]);
                    droppedTuples.Add(currentSolution[i + 3]);
                    droppedTuples.Add(currentSolution[i + 4]);
                    droppedTuples.Add(currentSolution[i + 5]);
                    droppedTuples.Add(currentSolution[i + 6]);
                    droppedTuples.Add(currentSolution[i + 7]);
                }
                else
                {
                    tempList.Add(currentSolution[i]);
                    tempList.Add(currentSolution[i + 1]);
                    tempList.Add(currentSolution[i + 2]);
                    tempList.Add(currentSolution[i + 3]);
                    tempList.Add(currentSolution[i + 4]);
                    tempList.Add(currentSolution[i + 5]);
                    tempList.Add(currentSolution[i + 6]);
                    tempList.Add(currentSolution[i + 7]);
                }
            }
            currentSolution = tempList;

            for (var k = droppedTuples.Count; (k -= 8) >= 0;)
            {
                var saveInHistory = true;
                for (var j = history.Count; saveInHistory && (j -= 8) >= 0;)
                {
                    if (droppedTuples[k] == history[j] && droppedTuples[k + 1] == history[j + 1] &&
                        droppedTuples[k + 2] == history[j + 2] && droppedTuples[k + 3] == history[j + 3] &&
                        droppedTuples[k + 4] == history[j + 4] && droppedTuples[k + 5] == history[j + 5])
                    {
                        saveInHistory = false;
                    }
                }
                if (!saveInHistory) continue;
                history.Add(droppedTuples[k]);
                history.Add(droppedTuples[k + 1]);
                history.Add(droppedTuples[k + 2]);
                history.Add(droppedTuples[k + 3]);
                history.Add(droppedTuples[k + 4]);
                history.Add(droppedTuples[k + 5]);
                history.Add(droppedTuples[k + 6]);
                history.Add(droppedTuples[k + 7]);
            }

            using (var enumerator = destinationSelected.GetEnumerator())
            {
                while (enumerator.MoveNext())
                    SolveTasks(currentSolution, enumerator.Current);
            }

            var tempFitness = ObjectiveFunction(currentSolution);
            if (tempFitness < bestFitness)
            {
                return new Tuple<List<int>, int>(currentSolution, tempFitness);
            }
           
            for (var i = droppedIndices.Length; i-- > 0;)
            {
                if (!droppedIndices[i]) continue;
                var offset = i * 8;
                for (var j = history.Count; (j -= 8) >= 0;)
                {
                    if (movings[offset] == history[j] && movings[offset + 1] == history[j + 1] &&
                        movings[offset + 2] == history[j + 2] && movings[offset + 3] == history[j + 3] &&
                        movings[offset + 4] == history[j + 4] && movings[offset + 5] == history[j + 5])
                    {
                        history[j + 6]++;
                        movings[offset + 6] = history[j + 6];
                        if (movings[offset + 6] >= SaturationBoundary)
                        {
                            movings[offset + 7] = 1;
                            history[j + 7] = 1;
                        }
                        break;
                    }
                }
            }
            return new Tuple<List<int>, int>(currentSolution, ObjectiveFunction(currentSolution));
        }


        /// <summary>
        ///     Computes the Greedy Randomized Adaptive Search Procedure Methaheuristic.
        ///     <para />
        ///     Whereas the number of required users for each type and each cell is computed
        ///     in a deterministic way (through Change Making Problem), order of cells from
        ///     where compute the greedy is taken randomly
        /// </summary>
        /// <param name="requiredUsers">
        ///     Tree of required users:
        ///     <list type="bullet">
        ///         <item>Key: Destination cell</item>
        ///         <item>
        ///             <list type="bullet">
        ///                 <item>Key: User type</item>
        ///                 <item>Value: Number of required users</item>
        ///             </list>
        ///         </item>
        ///     </list>
        /// </param>
        /// <returns>Solution (if any) computed through greedy</returns>
        /// <exception cref="NoUserLeft">
        ///     Current GRASP used more user of
        ///     the available ones.
        /// </exception>
        public static List<int> GRASP(Dictionary<int, Dictionary<int, int>> requiredUsers)
        {
            // Optimization block (not really required, just more readability ed enforced inling)
            var costs = problem.Matrix;
            var availability = problem.Availability;
            var cells = problem.Cells;
            var userTypes = problem.UserTypes;
            var timeSlots = problem.TimeSlots;
            // end optimization block
            var movings = new List<int>(1000);
            var totCell = cells;
            var visited = new bool[totCell];
            var allVisited = false;
            for (var i = totCell; --i >= 0;)
            {
                int cell;
                do
                {
                    cell = generator.Next(0, totCell);
                    if (problem.Tasks[cell] != 0) continue;
                    visited[cell] = true;
                    allVisited = visited.All(v => v);
                } while (visited[cell] && !allVisited);
                if (allVisited) break;
                visited[cell] = true;
                // Clone in array
                var required = new Dictionary<int, int>(requiredUsers[cell]);
                // End clone
                var tasks = problem.Tasks[cell];
                for (var userType = userTypes; userType-- > 0;)
                    while (required.ContainsKey(userType) && required[userType] != 0)
                    {
                        if (problem.Users == 0)
                            throw new NoUserLeft();
                        var minimum = costs.GetMin(cell, userType);
                        var available =
                            availability[(minimum[0] * timeSlots + minimum[1]) * userTypes + userType];
                        if (available >= required[userType])
                        {
                            availability[(minimum[0] * timeSlots + minimum[1]) * userTypes + userType] -=
                                required[userType];
                            problem.Users -= required[userType];
                            movings.Add(minimum[0]); //start
                            movings.Add(cell); //destination
                            movings.Add(minimum[1]); //timeslot
                            movings.Add(userType); //usertype
                            movings.Add(required[userType]); //used users
                            movings.Add(tasks); //performed tasks
                            movings.Add(0);
                            movings.Add(0);
                            required[userType] = 0;
                        }
                        else
                        {
                            availability[(minimum[0] * timeSlots + minimum[1]) * userTypes + userType] -=
                                available;
                            problem.Users -= available;
                            tasks -= available * problem.TasksPerUser[userType].Tasks;
                            required[userType] -= available;
                            movings.Add(minimum[0]); //start
                            movings.Add(cell); //destination
                            movings.Add(minimum[1]); //timeslot
                            movings.Add(userType); //usertype
                            movings.Add(available); //used users
                            movings.Add(available * problem.TasksPerUser[userType].Tasks); //performed tasks
                            movings.Add(0);
                            movings.Add(0);
                        }
                    }
            }
            return movings;
        }
    }
}