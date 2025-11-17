using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Linear
{
    public partial class MainWindow : Window
    {
        private double[,] matrix;
        private int numRows;
        private int numCols;

        private int currentPivotRow;
        private int currentPivotCol;

        // The enumerator holds the state of the step-by-step algorithm
        private IEnumerator<string> algorithmIterator;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Parses the input, initializes the algorithm, and displays the starting matrix.
        /// </summary>
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ParseMatrix(MatrixInput.Text))
            {
                return; // Error message was already set by ParseMatrix
            }

            // Initialize the algorithm state
            currentPivotRow = 0;
            currentPivotCol = 0;
            algorithmIterator = PerformRrefSteps().GetEnumerator();

            // Update UI
            UpdateMatrixDisplay();
            StepLog.Text = "";
            StepDescription.Text = "Matrix loaded. Click 'Next Step' to find the first pivot.";
            StartButton.IsEnabled = false;
            MatrixInput.IsEnabled = false;
            NextStepButton.IsEnabled = true;
            ResetButton.IsEnabled = true;
        }

        /// <summary>
        /// Executes the next single step of the RREF algorithm.
        /// </summary>
        private void NextStepButton_Click(object sender, RoutedEventArgs e)
        {
            if (algorithmIterator == null) return;

            // MoveNext() executes the code in PerformRrefSteps until the next 'yield return'
            if (algorithmIterator.MoveNext())
            {
                string description = algorithmIterator.Current;
                StepDescription.Text = description;
                StepLog.Text += description + "\n";

                // The algorithm method modifies 'matrix', so we just need to re-render it
                UpdateMatrixDisplay();
            }
            else
            {
                // The algorithm is finished. The final status message was set
                // in the last successful MoveNext() call. We just disable the button.
                NextStepButton.IsEnabled = false;
                algorithmIterator = null;
            }
        }

        /// <summary>
        /// Resets the application to its initial state.
        /// </summary>
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            matrix = null;
            algorithmIterator = null;
            MatrixDisplayGrid.Children.Clear();
            MatrixDisplayGrid.RowDefinitions.Clear();
            MatrixDisplayGrid.ColumnDefinitions.Clear();

            MatrixInput.Text = "1 1 2 9\n2 4 -3 1\n3 6 -5 0";
            StepDescription.Text = "Enter an augmented matrix and press 'Start'.";
            StepLog.Text = "";

            StartButton.IsEnabled = true;
            MatrixInput.IsEnabled = true;
            NextStepButton.IsEnabled = false;
            ResetButton.IsEnabled = false;
        }

        /// <summary>
        /// This is the core algorithm, written as an "iterator" method.
        /// 'yield return' pauses the method and sends a description string back.
        /// </summary>
        private IEnumerable<string> PerformRrefSteps()
        {
            while (currentPivotRow < numRows && currentPivotCol < numCols)
            {
                // === STEP 1: Find first non-zero column ===
                yield return $"Finding pivot in Column {currentPivotCol + 1}, at or below Row {currentPivotRow + 1}.";

                int pivotRow = -1;
                for (int r = currentPivotRow; r < numRows; r++)
                {
                    // Use a small tolerance for floating point comparisons
                    if (Math.Abs(matrix[r, currentPivotCol]) > 1e-9)
                    {
                        pivotRow = r;
                        break;
                    }
                }

                if (pivotRow == -1)
                {
                    // This column is all zeros below the pivot, move to the next column
                    yield return $"Column {currentPivotCol + 1} has no pivot. Moving to next column.";
                    currentPivotCol++;
                    continue;
                }

                // === STEP 2: Swap to make pivot non-zero (if needed) ===
                if (pivotRow != currentPivotRow)
                {
                    yield return $"Pivot found at ({pivotRow + 1}, {currentPivotCol + 1}). Swapping R{currentPivotRow + 1} and R{pivotRow + 1}.";
                    SwapRows(currentPivotRow, pivotRow);
                    // Pause to show the result of the swap
                    yield return $"R{currentPivotRow + 1} and R{pivotRow + 1} swapped.";
                }
                else
                {
                    yield return $"Pivot found at ({currentPivotRow + 1}, {currentPivotCol + 1}). No swap needed.";
                }

                // === STEP 3: Make pivot = 1 ===
                double pivotValue = matrix[currentPivotRow, currentPivotCol];
                if (Math.Abs(pivotValue - 1.0) > 1e-9)
                {
                    yield return $"Scaling R{currentPivotRow + 1} by 1 / {pivotValue:F2} to make pivot = 1.";
                    ScaleRow(currentPivotRow, 1.0 / pivotValue);
                    yield return $"R{currentPivotRow + 1} scaled.";
                }
                else
                {
                    yield return "Pivot is already 1. No scaling needed.";
                }


                // === STEP 4: Make all other elements in column = 0 ===
                yield return $"Eliminating other entries in Column {currentPivotCol + 1}.";
                for (int r = 0; r < numRows; r++)
                {
                    if (r != currentPivotRow)
                    {
                        double factor = matrix[r, currentPivotCol];
                        if (Math.Abs(factor) > 1e-9)
                        {
                            yield return $"Eliminating in R{r + 1}:  R{r + 1} = R{r + 1} - ({factor:F2}) * R{currentPivotRow + 1}.";
                            AddRows(r, currentPivotRow, -factor);
                            yield return $"R{r + 1} updated.";
                        }
                    }
                }

                yield return $"Column {currentPivotCol + 1} is complete.";
                currentPivotRow++;
                currentPivotCol++;
            }

            yield return "RREF calculation complete. Analyzing system solution...";

            // === STEP 5: Analyze the RREF matrix for solutions ===
            // This assumes an augmented matrix where the last column is the constant.

            // Analysis only makes sense for an augmented matrix (at least 2 columns)
            if (numCols <= 1)
            {
                yield return "Matrix has only one column. Solution analysis is not applicable.";
                yield break; // Stop iterator
            }

            int numVariables = numCols - 1;
            int numPivots = currentPivotRow; // This is the number of non-zero rows, which is our pivot count.

            // Check for contradiction [ 0 0 ... 0 | b ] where b != 0
            bool hasContradiction = false;
            string contradictionRow = "";

            // We must check ALL rows for a contradiction
            for (int r = 0; r < numRows; r++)
            {
                bool allZerosInCoefficients = true;
                for (int c = 0; c < numVariables; c++)
                {
                    if (Math.Abs(matrix[r, c]) > 1e-9)
                    {
                        allZerosInCoefficients = false;
                        break;
                    }
                }

                bool nonZeroConstant = Math.Abs(matrix[r, numCols - 1]) > 1e-9;

                if (allZerosInCoefficients && nonZeroConstant)
                {
                    hasContradiction = true;
                    contradictionRow = $"R{r + 1}: [ 0 ... 0 | {matrix[r, numCols - 1]:F2} ]";
                    break;
                }
            }

            // --- Report results ---
            if (hasContradiction)
            {
                yield return $"Inconsistency found in {contradictionRow}.";
                yield return "This means 0 equals a non-zero number. The system has NO SOLUTION.";
            }
            else
            {
                yield return $"Analysis found {numPivots} pivot(s) for {numVariables} variable(s).";
                if (numPivots < numVariables)
                {
                    int freeVariables = numVariables - numPivots;
                    yield return $"There are {freeVariables} free variable(s). The system has an INFINITE number of solutions.";
                }
                else // numPivots == numVariables
                {
                    yield return "There are no free variables. The system has a UNIQUE SOLUTION.";
                }
            }

            yield return "Analysis complete.";
        }


        #region Matrix Operations

        // Swaps row r1 and row r2
        private void SwapRows(int r1, int r2)
        {
            for (int c = 0; c < numCols; c++)
            {
                (matrix[r1, c], matrix[r2, c]) = (matrix[r2, c], matrix[r1, c]);
            }
        }

        // Multiplies row r by a scalar factor
        private void ScaleRow(int r, double factor)
        {
            for (int c = 0; c < numCols; c++)
            {
                matrix[r, c] *= factor;
            }
        }

        // Adds 'factor' * 'sourceRow' to 'targetRow'
        // R_target = R_target + (factor * R_source)
        private void AddRows(int targetRow, int sourceRow, double factor)
        {
            for (int c = 0; c < numCols; c++)
            {
                matrix[targetRow, c] += factor * matrix[sourceRow, c];
            }
        }

        #endregion

        #region UI and Parsing

        /// <summary>
        /// Reads the text from the TextBox and populates the 'matrix' array.
        /// </summary>
        private bool ParseMatrix(string text)
        {
            try
            {
                string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                numRows = lines.Length;
                if (numRows == 0) throw new Exception("No rows found.");

                string[] firstRow = lines[0].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                numCols = firstRow.Length;
                if (numCols == 0) throw new Exception("No columns found.");

                matrix = new double[numRows, numCols];

                for (int r = 0; r < numRows; r++)
                {
                    string[] parts = lines[r].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != numCols) throw new Exception($"Row {r + 1} has {parts.Length} columns, but expected {numCols}.");

                    for (int c = 0; c < numCols; c++)
                    {
                        if (!double.TryParse(parts[c], out matrix[r, c]))
                        {
                            throw new Exception($"Invalid number '{parts[c]}' at Row {r + 1}, Column {c + 1}.");
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                StepDescription.Text = $"Error parsing matrix: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Clears and redraws the matrix display grid based on the 'matrix' array.
        /// </summary>
        private void UpdateMatrixDisplay()
        {
            MatrixDisplayGrid.Children.Clear();
            MatrixDisplayGrid.RowDefinitions.Clear();
            MatrixDisplayGrid.ColumnDefinitions.Clear();

            // Create row and column definitions
            for (int r = 0; r < numRows; r++)
            {
                MatrixDisplayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            for (int c = 0; c < numCols; c++)
            {
                MatrixDisplayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            // Create TextBlocks for each cell
            for (int r = 0; r < numRows; r++)
            {
                for (int c = 0; c < numCols; c++)
                {
                    // Clean up near-zero numbers for cleaner display
                    double cellValue = matrix[r, c];
                    if (Math.Abs(cellValue) < 1e-9) cellValue = 0;

                    var tb = new TextBlock
                    {
                        Text = cellValue.ToString("F2"), // Format to 2 decimal places
                        Margin = new Thickness(10),
                        Padding = new Thickness(8),
                        MinWidth = 60,
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 16,
                        FontWeight = FontWeights.Medium,
                        Background = Brushes.WhiteSmoke,
                    };

                    // --- Visual Highlighting ---
                    // Highlight the row/col we are working on
                    if (NextStepButton.IsEnabled && r == currentPivotRow && c == currentPivotCol)
                    {
                        // Current Pivot
                        tb.Background = Brushes.Gold;
                        tb.FontWeight = FontWeights.Bold;
                    }
                    else if (NextStepButton.IsEnabled && r == currentPivotRow)
                    {
                        // Current Pivot Row
                        tb.Background = Brushes.LightCyan;
                    }
                    else if (NextStepButton.IsEnabled && c == currentPivotCol)
                    {
                        // Current Pivot Column
                        tb.Background = Brushes.LightCyan;
                    }
                    else
                    {
                        // Highlight the final "answer" column after completion
                        if (!NextStepButton.IsEnabled && numCols > 1 && c == numCols - 1)
                        {
                            tb.Background = Brushes.LightGreen;
                        }
                        // Highlight pivot columns (where a 1 is)
                        if (!NextStepButton.IsEnabled && c < numCols - 1 && Math.Abs(cellValue - 1.0) < 1e-9)
                        {
                            tb.Background = Brushes.LightBlue;
                            tb.FontWeight = FontWeights.Bold;
                        }
                    }


                    Grid.SetRow(tb, r);
                    Grid.SetColumn(tb, c);
                    MatrixDisplayGrid.Children.Add(tb);
                }
            }
        }
        #endregion
    }
}