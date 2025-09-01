using System;
using System.CodeDom.Compiler;
using System.IO;
using Newtonsoft.Json;
using static System.Net.Mime.MediaTypeNames;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Wordle_Solver
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var json = File.ReadAllText("Words.txt");
            string[] words = JsonConvert.DeserializeObject<string[]>(json);

            json = File.ReadAllText("ValidWords.txt");
            string[] validWords = JsonConvert.DeserializeObject<string[]>(json);

            char[] correct = [' ', ' ', 'a', ' ', 't'];
            char[][] misplaced = [['e'], [], [], ['t', 'l'], ['e', 's']];
            string wrongString = "roncbyr";

            /*
            char[] correct = [' ', ' ', ' ', ' ', ' '];
            char[][] misplaced = [[], [], [], [], []];
            string wrongString = "";
            */

            WordFilter filter = new WordFilter(correct, misplaced, wrongString);

            List<string> filteredWords = filterWords(filter, validWords);

            string[] filteredWordsArray = filteredWords.ToArray();

            for (int i = 0; i < filteredWords.Count; i++)
            {
                Console.WriteLine(filteredWords[i]);
            }
            Console.WriteLine(filteredWords.Count());
            Console.WriteLine();
            Console.WriteLine(words.Count());

            //Create a dictionary
            ConcurrentDictionary<string, float> keyValuePairs = new ConcurrentDictionary<string, float>();
            Task<float>[] outputs = new Task<float>[words.Length];

            //Loop over every possible guess
            for (int i = 0; i < words.Count(); i++)
            {
                outputs[i] = countAvgRemaining(words[i], filteredWordsArray);
            }

            for (int i = 0; i < words.Count(); i++)
            {
                keyValuePairs[words[i]] = outputs[i].Result;
            }

            //Sort dictonary by key first (alphabetical order), then by value
            var sortedDict = keyValuePairs.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value).ToArray();
            sortedDict = sortedDict.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value).ToArray();

            for (int i = 0; i < Math.Min(100, sortedDict.Length); i++)
            {
                Console.WriteLine(sortedDict[i].Key + "\t" + sortedDict[i].Value);
            }
        }

        public static List<string> filterWords(WordFilter filter, string[] wordList)
        {
            char[] correct = filter.correct;
            char[][] misplaced = filter.misplaced;
            char[] wrong = filter.wrong;

            List<string> filtered = new List<string>();

            //Check each word in the given word list
            for (int i = 0; i < wordList.Length; i++)
            {
                bool possibleWord = true;
                string word = wordList[i];

                //Check if all the correct letters are present
                for (int j = 0; j < correct.Length; j++)
                {
                    //Check if the correct letter is unknown, if so, skip (a space represents unknown)
                    if (correct[j] == ' ') continue;

                    if (correct[j] != word[j])
                    {
                        possibleWord = false;
                        break;
                    }
                }
                if (!possibleWord) continue;

                //Check if all the misplaced letters are present
                for (int j = 0; j < misplaced.Length; j++)
                {
                    for (int k = 0; k < misplaced[j].Length; k++)
                    {
                        //Check that the letter is not in the same location
                        if (misplaced[j][k] == word[j])
                        {
                            possibleWord = false;
                            break;
                        }

                        //Check if the letter is present in the word
                        if (word.IndexOf(misplaced[j][k]) == -1)
                        {
                            possibleWord = false;
                            break;
                        }
                    }

                    if (!possibleWord) break;
                }
                if (!possibleWord) continue;

                //Check if the word contains any wrong letters
                for (int j = 0; j < wrong.Length; j++)
                {
                    if (word.IndexOf(wrong[j]) != -1)
                    {
                        possibleWord = false;
                        break;
                    }
                }
                if (!possibleWord) continue;

                filtered.Add(word);
            }

            return filtered;
        }

        public static async Task<float> countAvgRemaining(string word, string[] wordList)
        {
            return await Task.Run(() => {

                float sum = 0;

                //Loop over every possible answer
                for (int i = 0; i < wordList.Length; i++)
                {
                    //Generate a filter based on the possible solution and guess
                    WordFilter tempFilter = new WordFilter(wordList[i], word);

                    //Add the number of remaining words with the given filter
                    sum += filterWords(tempFilter, wordList).Count();
                }

                sum /= wordList.Length;

                return sum;
            });

        }
    }

    public class WordFilter
    {
        public char[] correct;
        public char[][] misplaced;
        public char[] wrong;

        //Nullable constructor
        public WordFilter(char[] correct = null, char[][] misplaced = null, string wrongString = "")
        {
            this.correct = correct ?? [' ', ' ', ' ', ' ', ' '];
            this.misplaced = misplaced ?? [[], [], [], [], []];

            wrong = wrongString.ToCharArray();
        }

        //Constructor that combines two filters into one
        public WordFilter(WordFilter a, WordFilter b)
        {
            correct = [' ', ' ', ' ', ' ', ' '];
            misplaced = [[], [], [], [], []];
            wrong = [];

            for (int i = 0; i < a.correct.Length; i++)
            {
                //Check if both filters have correct letters that don't match
                if (a.correct[i] != ' ' && b.correct[i] != ' ' && a.correct[i] != b.correct[i])
                {
                    throw new Exception("Conflicting correct letters.");
                }

                if (a.correct[i] != ' ') correct[i] = a.correct[i];
                if (b.correct[i] != ' ') correct[i] = b.correct[i];

                misplaced[i] = a.misplaced[i].Union(b.misplaced[i]).ToArray();

                wrong = a.wrong.Union(b.wrong).ToArray();
            }
        }

        //Constructor that combines multiple filters into one filter
        public WordFilter(WordFilter[] filters)
        {
            WordFilter combined = new WordFilter();

            for (int i = 0; i < filters.Length; i++)
            {
                combined = new WordFilter(combined, filters[i]);
            }
        }

        //Constructor that gives the resulting filter of a guess to a given solution
        public WordFilter(string solution, string guess)
        {
            correct = [' ', ' ', ' ', ' ', ' '];
            misplaced = [[], [], [], [], []];
            wrong = [];


            for (int i = 0; i < solution.Length; i++)
            {
                //Get correct letters
                if (solution[i] == guess[i])
                {
                    correct[i] = guess[i];
                }
                else
                {
                    //If the letter is not in the correct position, check if it is in the word
                    if (solution.IndexOf(guess[i]) != -1)
                    {
                        misplaced[i] = [guess[i]];
                    }
                    else
                    {
                        //If the letter is not present in the word, add it to the list of wrong letters
                        wrong = wrong.Union([guess[i]]).ToArray();
                    }
                }
            }
        }
    }
}