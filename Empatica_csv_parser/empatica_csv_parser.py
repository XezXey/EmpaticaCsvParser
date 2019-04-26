import argparse
import csv
import json
import re
import os 
import errno

#features_header = ['E4_Acc', 'E4_Bvp', 'E4_Gsr', 'E4_Ibi', 'E4_Hr', 'E4_Temperature', 'E4_Tag', 'E4_Battery']
feature_list = ['Acc', 'Bvp', 'Gsr', 'Ibi', 'Hr', 'Tmp', 'Tag', 'Batt']
feature_header = {'Acc' : ['TS_Machine', 'ax', 'ay', 'az'], 
                  'Bvp' : ['TS_Machine', 'bvp'],
                  'Gsr' : ['TS_Machine', 'gsr'], 
                  'Ibi' : ['TS_Machine', 'ibi'], 
                  'Hr' : ['TS_Machine', 'hr'], 
                  'Tmp' : ['TS_Machine', 'tmp'], 
                  'Tag' : ['TS_Machine', 'tag'], 
                  'Batt' : ['TS_Machine', 'batt']}

def get_writer(experiment_datetime, subject_no):
    # Create a writer for each feature per file
    writer = {}
    sj_path = "./parsered/{0}/".format(subject_no)
    os.makedirs(os.path.dirname(sj_path), exist_ok=True)
    feature_fn_path = ["{0}{1}_{2}{3}.csv".format(sj_path, subject_no, fn, experiment_datetime) for fn in feature_list]
    print(feature_fn_path)
    for index, each_feature in enumerate(feature_list):
        writer[each_feature] = csv.writer(open(feature_fn_path[index], 'w', newline=''))
        writer[each_feature].writerow(feature_header[each_feature])
    return writer

def process(input_file_name, subject_no):
    # Process the empatica file and convert into .csv format
    with open(input_file_name, 'r') as src_file: # Reading a empatica input file
        experiment_datetime = src_file.readline().strip('\n')
        writer = get_writer(experiment_datetime, subject_no)
        src_lines = src_file.readlines() # Read all lines from input file
        for each_line in src_lines:
            each_line = each_line.strip('\n').split(' ')
            feature_name = each_line[0]
            if feature_name == 'E4_Acc':
                # Accelerometer
                ts = each_line[1]
                ax = each_line[2]
                ay = each_line[3]
                az = each_line[4]
                writer['Acc'].writerow([ts, ax, ay, az])

            elif feature_name == 'E4_Bvp':
                # Blood volume pressure
                ts = each_line[1]
                bvp = each_line[2]
                writer['Bvp'].writerow([ts, bvp])

            elif feature_name == 'E4_Gsr':
                # Galvanic skin resistance
                ts = each_line[1]
                gsr = each_line[2]
                writer['Gsr'].writerow([ts, gsr])

            elif feature_name == 'E4_Temperature':
                # Temperature
                ts = each_line[1]
                temperature = each_line[2]
                writer['Tmp'].writerow([ts, temperature])

            elif feature_name == 'E4_Tag':
                # Tag
                ts = each_line[1]
                tag = each_line[2]
                writer['Tag'].writerow([ts, tag])

            elif feature_name == 'E4_Hr':
                # Heart rate
                ts = each_line[1]
                hr = each_line[2]
                writer['Hr'].writerow([ts, hr])

            elif feature_name == 'E4_Ibi':
                # Inter-beat interval
                ts = each_line[1]
                ibi = each_line[2]
                writer['Ibi'].writerow([ts, ibi])

            elif feature_name == 'E4_battery':
                # Battery level
                ts = each_line[1]
                battery = each_line[2]
                writer['Batt'].writerow([ts, battery])
            else : 
                # Not E4 data packets
                continue


if __name__ == '__main__':
    parser = argparse.ArgumentParser() # Adding infile and outfile using AgrumentParser
    parser.add_argument('--infile', required=True, type=str, nargs='+') # Input empatica data file
    parser.add_argument('--outfile', type=str, nargs='+') # Output file(If needed)

    args = parser.parse_args()
    sj_txt_pattern = re.compile('(subject)[0-9]{1,}')
    
    for i in range(len(args.infile)): # Iterate over files to process
        subject_no = re.search(sj_txt_pattern, args.infile[i]).group(0)
        process(args.infile[i], subject_no)
