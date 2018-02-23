using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace QuaternionFilter
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("PROGRAM START");

            

            var filter = new Filter(@"C:\Users\Alexander Wolff\Documents\GitHub\Calibration Program\raw_data\Alex\brush\input.csv",
                                    @"C:\Users\Alexander Wolff\Documents\GitHub\Calibration Program\raw_data\Alex\brush\filtered.csv");

            Console.WriteLine("PROGRAM END");
            Console.WriteLine("PRESS ANY KEY TO CLOSE");
            Console.ReadKey();
        }

        
    }

    class Filter
    {
        public Filter(string inpath, string outpath, int window_size = 10, int ignore_lines = 100)
        {
            //LOAD
            var input_data = read_file(inpath);

            //SEGMENT
            var segmented = segment_events(input_data);

            //FILTER
            List<float[][]> output = new List<float[][]>();
            foreach (var segment in segmented)
            {
                output.Add(filter_section(segment, window_size));
            }

            //IGNORE LINES
            output[0] = ignore_calibration_lines(output[0], ignore_lines);

            //RECONSTRUCT
            var repeat_event_format = File.ReadAllLines(inpath)[copy_repeat_event(input_data, 10)];
            var formatted_output = reconstruct_file(output, repeat_event_format);

            //WRITE TO FILE
            write_file(outpath, formatted_output);
        }

        /// <summary>
        /// Open file, read all lines, close file, return lines
        /// </summary>
        /// <param name="filepath">file location</param>
        /// <returns>file contents</returns>
        public static List<float[]> read_file(string filepath)
        {
            var raw_data = File.ReadAllLines(filepath);

            List<float[]> data = new List<float[]>();

            foreach(var line in raw_data)
            {
                List<float> parsed = new List<float>();

                foreach (var number in segment_line(line))
                {
                    parsed.Add(float.Parse(number));
                }

                data.Add(parsed.ToArray());
            }

            return data;
        }

        static List<int> identify_separators(string input, char separator)
        {
            List<int> separators = new List<int>();

            for(int i = 0; i < input.Length; i ++)
            {
                if (input[i] == separator) separators.Add(i);
            }

            return separators;
        }

        static string[] segment_line(string line, char separator = ',')
        {
            List<string> segments = new List<string>();
            int start_index = 0;

            List<int> separators = identify_separators(line, separator);
            separators.Add(line.Length);

            foreach (int separation in separators)
            {
                int segment_length = separation - start_index;

                string segment = line.Substring(start_index, segment_length);

                if(segment.Length > 0) segments.Add(segment);

                //start of new segment
                start_index = separation + 1;
            }

            return segments.ToArray();
        }


        //COMPARE
        static List<int> contains_repeat_event(List<float[]> data, float repeat_event)
        {
            List<int> separator_index = new List<int>();

            int len = data.Count;

            for(int i = 0; i < len; i++ )
            {
                if (is_repeat_event(data[i], repeat_event)) separator_index.Add(i);
            }

            return separator_index;
        }


        static bool is_repeat_event(float[] input, float repeat)
        {
            int repeat_count = 0;

            foreach(float i in input)
            {
                if (i == repeat) repeat_count++;
            }

            return repeat_count == input.Length;
        }

        static int copy_repeat_event(List<float[]> data, float repeat_event)
        {
            List<int> separator_index = new List<int>();

            int len = data.Count;

            for (int i = 0; i < len; i++)
            {
                if (is_repeat_event(data[i], repeat_event)) return i;
            }

            return 0;
        }

        //SEGMENT INTO SECTIONS (Repeat events as separators)
        static List<float[][]> segment_events(List<float[]> full_data, float repeat_event = 10)
        {
            List<float[][]> segments = new List<float[][]>();
            int start_index = 0;


            var separators = contains_repeat_event(full_data, repeat_event);

            //Add extra separator  to get to end of data
            separators.Add(full_data.Count);
            

            foreach( int separation in separators )
            {
                //non inclusive
                int segment_length = (separation - 1) - start_index;

                List<float[]> segment = cut_out(full_data, start_index, segment_length); 

                if(segment.Count > 0) segments.Add(segment.ToArray());

                //start of new segment
                start_index = separation + 1;
            }

            return segments;
        }

        static List<float[]> cut_out(List<float[]> input, int start, int length)
        {
            List<float[]> output = new List<float[]>();
            int end = start + length;

            for(int i = start; i < end; i++)
            {
                output.Add(input[i]);
            }

            return output;
        }
        
        //FILTER SECTIONS
        static float[][] filter_section(float[][] input_data, int window_size)
        {
            //work out which columns to ignore : 4 (quaternion) + 1 (MMG)
            int max = input_data[0].Length % 5;

            int columns = input_data[0].Length;
            int rows = input_data.Length;

            for (int column = 0; column < columns; column++)
            {
                //ignore non quaternion columns
                if ((column % 5 == 0) && (column > max)) continue;

                //extract
                var trace = extract_column(input_data, column);

                //filter
                var filtered = filter_trace(trace, window_size);

                //swap-out
                input_data = swap_out_column(input_data, filtered, column);   
            }

            return input_data;
        }

        static float[] extract_column(float[][] input, int column_index)
        {
            float[] column = new float[input.Length];

            for(int i = 0; i < input.Length; i++)
            {
                column[i] = input[i][column_index];
            }

            return column;
        }

        static float[] filter_trace(float[] trace, int window_size)
        {
            return moving_average(trace, window_size);
        }

        static float[] moving_average(float[] trace, int window_size)
        {
            float[] output = new float[trace.Length];

            for(int i = window_size/2; i < trace.Length - window_size/2; i++)
            {
                float sum = 0;
                for(int j = -window_size/2; j < window_size/2; j++)
                {
                    sum += trace[i + j];
                }
                sum /= window_size;
                sum = (float)Math.Round(sum, 3);

                output[i] = sum;
            }

            //pad edges
            for(int i = 0; i < window_size/2; i++)
            {
                output[i] = output[window_size / 2];
                output[(output.Length - 1) - i] = output[(output.Length - 1) - window_size / 2];
            }

            return output;
        }

        static float[][] swap_out_column(float[][] data, float[] column, int column_index)
        {
            for(int i = 0; i < column.Length; i++)
            {
                data[i][column_index] = column[i];
            }

            return data;
        }

        //RECONSTRUCT INTO SINGLE FILE
        static string[] reconstruct_file(List<float[][]> data, string repeat_event_format)
        {

            List<string[]> segments = new List<string[]>();

            foreach(var segment in data)
            {
                segments.Add(reconstruct_segment(segment));
            }

            while(segments.Count > 1)
            {
                var new_segment = merge_segments(segments[0], segments[1], repeat_event_format);

                segments[0] = new_segment;
                segments.RemoveAt(1);
            }
            return append_line(segments[0], repeat_event_format);
        }

        static string[] reconstruct_segment(float[][] data)
        {
            List<string> output = new List<string>();

            foreach(var row in data)
            {
                string formatted_row = "";

                for( int i = 0; i < row.Length; i++ )
                {
                    formatted_row += row[i].ToString();

                    if (i < row.Length-1) formatted_row += ",";
                }

                output.Add(formatted_row);
            }
            
            return output.ToArray();
        }

        static string[] append_line(string[] data, string line)
        {
            List<string> output = new List<string>();

            foreach (var value in data) output.Add(value);
            output.Add(line);

            return output.ToArray();
        }

        static string[] merge_segments(string[] segmentA, string[] segmentB, string separator)
        {
            List<string> merged = new List<string>();

            foreach(var value in segmentA)
            {
                merged.Add(value);
            }
            merged.Add(separator);
            foreach(var value in segmentB)
            {
                merged.Add(value);
            }

            return merged.ToArray();
        }

        static float[][] ignore_calibration_lines(float[][] data, int ignore_lines)
        {
            List<float[]> trimmed_data = new List<float[]>();

            for (int i = ignore_lines; i < data.Length; i++)
            {
                trimmed_data.Add(data[i]);
            }

            return trimmed_data.ToArray();
        }

        //WRITE
        static void write_file(string filepath, string[] data)
        {
            File.WriteAllLines(filepath, data);
        }
    }
}
