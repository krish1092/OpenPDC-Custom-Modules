using System;
using System.Collections.Generic;
using System.Linq;
using GSF.TimeSeries;
using PhasorProtocolAdapters;
using System.ComponentModel;

namespace CustomMeasurementActionAdapter
{
    [Description("CustomActionAdapter: Krishnan")]
    class CustomActionAdapter : CalculatedMeasurementBase
    {

        // Fields

        //The variable used to check if the event qualifies or not, for certain calculation
        private double event_qualifier = 0;

        //Number of frames; useful for debugging
        private int numberOfFrames;

        //Window Length
        private int window_length = 25;

        //Dictionary to keep track of queue of each measurement
        private Dictionary<Guid, Queue<double>> measurement_dictionary;

        //Dictionary to keep track of average value at a given time, for the measurement
        private Dictionary<Guid, double> measurement_average_dictionary;

        //Dictionary to store initial window's average value per measurement
        private Dictionary<Guid, double> measurement_initial_average_dictionary;

        //Dictionary to keep track of Lyapunov Exponent for each measurement
        private Dictionary<Guid, double> measurement_LE_dictionary;

        //Dictionary to keep track of average flag as to whether it has reached window length
        private Dictionary<Guid, bool> measurement_average_flag_dictionary;
        


        public override void Initialize()
        {
            base.Initialize();


            /// <span>Print to the console</span>
            OnStatusMessage("CustomActionAdapter has been successfully initialized...");

            //Getting the paramter that specifies the qualification value

            Dictionary<string, string> settings = Settings;
            string setting;
            
            if (settings.TryGetValue("event_qualifier", out setting))
                event_qualifier = Convert.ToDouble(setting);
            if (settings.TryGetValue("window_length", out setting))
                window_length = Convert.ToInt32(setting);

            measurement_dictionary = new Dictionary<Guid, Queue<double>>(2 * window_length);
            measurement_average_dictionary = new Dictionary<Guid, double>(2 * window_length);
            measurement_initial_average_dictionary = new Dictionary<Guid, double>(2 * window_length);
            measurement_LE_dictionary = new Dictionary<Guid, double>(2 * window_length);
            measurement_average_flag_dictionary = new Dictionary<Guid, bool>();

        }

      
       /// Create a list to store the input measurement keys
        List<string> inputMeasurementKeys = new List<string>();

        protected override void PublishFrame(IFrame frame, int index)
        {
            numberOfFrames++;

            if (numberOfFrames > 1000)
                numberOfFrames = 0;


            /// Prepare to clone the output measurements
            IMeasurement[] outputMeasurements = OutputMeasurements;

            List<IMeasurement> output = new List<IMeasurement>();
            output.Add(Measurement.Clone(
                       frame.Measurements.First().Value
                       , 1.0
                       , frame.Timestamp
                   ));

            foreach (IMeasurement measurement in frame.Measurements.Values)
            {
                /*output.Add(
                   Measurement.Clone(
                       measurement,
                       CalculateMovingAverageInMeasurement(measurement, window_length),
                       frame.Timestamp
                   ));
                   */
                inputMeasurementKeys.Add(measurement.Key.ToString());
                if (numberOfFrames % 30 == 0)
                {
                    //OnStatusMessage(measurement.Key.ToString() + ": " + measurement.Value.ToString());
                }
            }

            OnNewMeasurements(output);

        }



        


        /// <summary>
        /// Calculates moving average of <paramref name="measurement"/> and archives it.
        /// </summary>
        /// <param name="measurement">Measurements to be worked upon archived.</param>
        /// <param name="window_length">Window length</param>

        public double CalculateMovingAverageInMeasurement(IMeasurement measurement, int window_length)
        {
            
            Queue<double> measurement_queue;
            double moving_average_value;
            double measurement_initial_average_value;
            double measurement_LE_value;
            bool measurement_average_flag_value = false;
            if (!measurement_dictionary.TryGetValue(measurement.ID, out measurement_queue))
            {
                measurement_queue = new Queue<double>();
                measurement_dictionary.Add(measurement.ID, measurement_queue);
            }

            if (!measurement_average_dictionary.TryGetValue(measurement.ID, out moving_average_value))
            {
                measurement_average_dictionary.Add(measurement.ID, moving_average_value);
            }
            if (!measurement_initial_average_dictionary.TryGetValue(measurement.ID, out measurement_initial_average_value))
            {
                measurement_initial_average_dictionary.Add(measurement.ID, measurement_initial_average_value);
            }
            if (!measurement_LE_dictionary.TryGetValue(measurement.ID, out measurement_LE_value))
            {
                measurement_LE_dictionary.Add(measurement.ID, measurement_LE_value);
            }
            if (!measurement_average_flag_dictionary.TryGetValue(measurement.ID, out measurement_average_flag_value))
            {
                measurement_average_flag_dictionary.Add(measurement.ID, measurement_average_flag_value);
            }

            measurement_queue.Enqueue(measurement.AdjustedValue);
            if (measurement_queue.Count < window_length)
            {
                measurement_average_dictionary[measurement.ID] = 0;
                measurement_initial_average_dictionary[measurement.ID] = 0;
                measurement_LE_dictionary[measurement.ID] = 0;
                measurement_average_flag_dictionary[measurement.ID] = false;
                //measurement_average_flag_value = false;

            }
            if (measurement_queue.Count == window_length)
            {
                if (measurement_average_flag_dictionary[measurement.ID] == false)
                {
                    moving_average_value = measurement_queue.ToArray().Average();
                    measurement_average_dictionary[measurement.ID] = measurement_queue.ToArray().Average();
                    measurement_initial_average_dictionary[measurement.ID] = measurement_average_dictionary[measurement.ID];
                    measurement_average_flag_dictionary[measurement.ID] = true;
                    measurement_LE_dictionary[measurement.ID] = 0;
                    
                    //measurement_queue.Dequeue();
                }
            }
            if (measurement_queue.Count == window_length + 1)// the '+1' is important to ensure we do not discard the old value before subtracting
            {
                if (measurement_average_flag_dictionary[measurement.ID] == true)
                {
                    measurement_average_dictionary[measurement.ID] = measurement_average_dictionary[measurement.ID] + (measurement.AdjustedValue - measurement_queue.Dequeue()) / window_length;
                    moving_average_value = measurement_average_dictionary[measurement.ID];
                    measurement_LE_dictionary[measurement.ID] = measurement_average_dictionary[measurement.ID] - measurement_initial_average_dictionary[measurement.ID];
                   

                }
            
            }
            

            return moving_average_value;
        }



    }
}
